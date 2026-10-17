Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text

' ==================== ТИПЫ ====================

Public Enum StrType
    [String]
    [Integer]
    Binary
End Enum

' ==================== ЗАПИСЬ ====================

Public Class ListElement
    Private _name As String
    Private _type As StrType
    Private _value As Object

    Public Property Name
        Get
            Return _name
        End Get
        Set(value)
            _name = value
        End Set
    End Property

    Public Property Type
        Get
            Return _type
        End Get
        Set(value)
            _type = value
        End Set
    End Property

    Public Property Value
        Get
            Return _value
        End Get
        Set(value)
            _value = value
        End Set
    End Property

    Public Overrides Function ToString() As String
        Return $"{Name} : {Type} = {ValueToString()}"
    End Function

    Public Function ValueToString() As String
        If _value Is Nothing Then Return ""
        Select Case _type
            Case StrType.String
                Return CStr(_value)
            Case StrType.Integer
                Return Convert.ToString(_value, CultureInfo.InvariantCulture)
            Case StrType.Binary
                Dim bytes() As Byte = TryCast(_value, Byte())
                If bytes Is Nothing Then Return ""
                Return BitConverter.ToString(bytes).Replace("-", "")
            Case Else
                Return _value.ToString()
        End Select
    End Function

    Public Shared Function ParseValue(type As StrType, text As String) As Object
        Select Case type
            Case StrType.String
                Return text
            Case StrType.Integer
                Dim n As Integer
                If Not Integer.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, n) Then
                    Throw New FormatException($"Не целое число: ""{text}""")
                End If
                Return n
            Case StrType.Binary
                If String.IsNullOrEmpty(text) Then Return New Byte() {}
                If text.Length Mod 2 <> 0 Then
                    Throw New FormatException($"Hex нечётной длины: ""{text}""")
                End If
                Dim bytes(text.Length \ 2 - 1) As Byte
                For i As Integer = 0 To bytes.Length - 1
                    Dim b As Byte
                    If Not Byte.TryParse(text.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, b) Then
                        Throw New FormatException($"Неверный hex: ""{text}""")
                    End If
                    bytes(i) = b
                Next
                Return bytes
            Case Else
                Return text
        End Select
    End Function
End Class

' ==================== ТРАНЗАКЦИЯ ====================

Public Class MinilistTransaction
    Friend _snapshot As List(Of ListElement)
    Friend _owner As Minilist
    Public Property IsCompleted As Boolean = False

    Friend Sub New(owner As Minilist, snapshot As List(Of ListElement))
        _owner = owner
        _snapshot = snapshot
    End Sub
End Class

' ==================== БАЗА ====================

Public Class Minilist
    Implements IEnumerable(Of ListElement)

    Private _list As New List(Of ListElement)
    Private _index As New Dictionary(Of String, ListElement)(StringComparer.OrdinalIgnoreCase)
    Private _transaction As MinilistTransaction = Nothing

    ' ---------- СВОЙСТВА ----------

    Public ReadOnly Property Count As Integer
        Get
            Return _list.Count
        End Get
    End Property

    Default Public ReadOnly Property Item(index As Integer) As ListElement
        Get
            Return _list(index)
        End Get
    End Property

    Public ReadOnly Property InTransaction As Boolean
        Get
            Return _transaction IsNot Nothing AndAlso Not _transaction.IsCompleted
        End Get
    End Property

    ' ---------- CRUD ----------

    Public Sub AddElement(name As String, type As StrType, obj As ListElement)
        If String.IsNullOrWhiteSpace(name) Then
            Throw New ArgumentException("Имя не может быть пустым.", NameOf(name))
        End If
        If obj Is Nothing Then
            Throw New ArgumentNullException(NameOf(obj))
        End If
        If _index.ContainsKey(name) Then
            Throw New InvalidOperationException($"Дубликат ключа: ""{name}"" уже существует.")
        End If

        obj.Name = name
        obj.Type = type

        _list.Add(obj)
        _index(name) = obj
    End Sub

    Public Function GetByName(name As String) As ListElement
        Dim el As ListElement = Nothing
        If _index.TryGetValue(name, el) Then Return el
        Return Nothing
    End Function

    Public Function Contains(name As String) As Boolean
        Return _index.ContainsKey(name)
    End Function

    Public Sub UpdateValue(name As String, value As Object)
        Dim el As ListElement = GetByName(name)
        If el Is Nothing Then
            Throw New KeyNotFoundException($"Не найдено: ""{name}""")
        End If
        el.Value = value
    End Sub

    Public Sub Rename(oldName As String, newName As String)
        If String.IsNullOrWhiteSpace(newName) Then
            Throw New ArgumentException("Новое имя не может быть пустым.", NameOf(newName))
        End If
        Dim el As ListElement = GetByName(oldName)
        If el Is Nothing Then
            Throw New KeyNotFoundException($"Не найдено: ""{oldName}""")
        End If
        If _index.ContainsKey(newName) Then
            Throw New InvalidOperationException($"Дубликат ключа: ""{newName}"" уже существует.")
        End If

        _index.Remove(oldName)
        el.Name = newName
        _index(newName) = el
    End Sub

    Public Sub ChangeType(name As String, newType As StrType)
        Dim el As ListElement = GetByName(name)
        If el Is Nothing Then
            Throw New KeyNotFoundException($"Не найдено: ""{name}""")
        End If
        el.Type = newType
    End Sub

    Public Sub RemoveElement(name As String)
        Dim el As ListElement = GetByName(name)
        If el IsNot Nothing Then
            _list.Remove(el)
            _index.Remove(name)
        End If
    End Sub

    Public Sub Clear()
        _list.Clear()
        _index.Clear()
    End Sub

    ' ---------- ТРАНЗАКЦИИ ----------

    Public Function BeginTransaction() As MinilistTransaction
        If InTransaction Then
            Throw New InvalidOperationException("Транзакция уже открыта.")
        End If
        Dim snapshot As New List(Of ListElement)(_list)
        _transaction = New MinilistTransaction(Me, snapshot)
        Return _transaction
    End Function

    Public Sub Commit()
        If Not InTransaction Then
            Throw New InvalidOperationException("Нет открытой транзакции.")
        End If
        _transaction.IsCompleted = True
        _transaction = Nothing
    End Sub

    Public Sub Rollback()
        If Not InTransaction Then
            Throw New InvalidOperationException("Нет открытой транзакции.")
        End If
        _list.Clear()
        _index.Clear()
        For Each el As ListElement In _transaction._snapshot
            _list.Add(el)
            _index(el.Name) = el
        Next
        _transaction.IsCompleted = True
        _transaction = Nothing
    End Sub

    ' ---------- CSV: ЭКРАНИРОВАНИЕ ----------

    ' Поле оборачивается в кавычки, если содержит запятую, кавычку, CR или LF.
    ' Внутри кавычек кавычка удваивается: "" -> ""
    Private Shared Function CsvEscape(s As String) As String
        If s Is Nothing Then Return """"""
        Dim needQuotes As Boolean = s.IndexOf(","c) >= 0 OrElse
                                    s.IndexOf(""""c) >= 0 OrElse
                                    s.IndexOf(vbCr) >= 0 OrElse
                                    s.IndexOf(vbLf) >= 0
        If Not needQuotes Then Return s
        Return """" & s.Replace("""", """""") & """"
    End Function

    ' Разбор CSV-строки с учётом кавычек. Возвращает поля.
    Private Shared Function CsvSplitLine(line As String) As List(Of String)
        Dim result As New List(Of String)
        Dim sb As New StringBuilder()
        Dim inQuotes As Boolean = False
        Dim i As Integer = 0

        While i < line.Length
            Dim c As Char = line(i)

            If inQuotes Then
                If c = """"c Then
                    If i + 1 < line.Length AndAlso line(i + 1) = """"c Then
                        sb.Append(""""c)
                        i += 2
                    Else
                        inQuotes = False
                        i += 1
                    End If
                Else
                    sb.Append(c)
                    i += 1
                End If
            Else
                If c = """"c Then
                    inQuotes = True
                    i += 1
                ElseIf c = ","c Then
                    result.Add(sb.ToString())
                    sb.Clear()
                    i += 1
                Else
                    sb.Append(c)
                    i += 1
                End If
            End If
        End While

        result.Add(sb.ToString())
        Return result
    End Function

    ' ---------- CSV: СЕРИАЛИЗАЦИЯ ----------

    ' Формат: Name,Type,Value   (запятая-разделитель, кавычки для экранирования)
    Public Function ToCsv() As String
        Dim sb As New StringBuilder()
        For Each el As ListElement In _list
            sb.Append(CsvEscape(el.Name)).Append(","c)
            sb.Append(el.Type.ToString()).Append(","c)
            sb.Append(CsvEscape(el.ValueToString())).AppendLine()
        Next
        Return sb.ToString()
    End Function

    Public Sub FromCsv(text As String)
        Dim newList As New List(Of ListElement)()
        Dim newIndex As New Dictionary(Of String, ListElement)(StringComparer.OrdinalIgnoreCase)

        If String.IsNullOrEmpty(text) Then
            _list = newList
            _index = newIndex
            Return
        End If

        ' Разбиваем на строки, но с учётом кавычек: LF внутри кавычек — часть поля.
        Dim records As New List(Of String)()
        Dim sb As New StringBuilder()
        Dim inQuotes As Boolean = False

        Dim i As Integer = 0
        While i < text.Length
            Dim c As Char = text(i)
            If c = """"c Then
                inQuotes = Not inQuotes
                sb.Append(c)
                i += 1
            ElseIf Not inQuotes AndAlso c = vbLf Then
                records.Add(sb.ToString())
                sb.Clear()
                i += 1
            ElseIf Not inQuotes AndAlso c = vbCr Then
                ' CRLF: пропускаем CR, LF обработается следующим
                i += 1
            Else
                sb.Append(c)
                i += 1
            End If
        End While
        If sb.Length > 0 Then records.Add(sb.ToString())

        Dim lineNo As Integer = 0
        For Each line As String In records
            lineNo += 1
            If line.Length = 0 Then Continue For
            If line.StartsWith("#") Then Continue For

            Dim parts As List(Of String) = CsvSplitLine(line)
            If parts.Count < 2 Then
                Throw New FormatException($"Строка {lineNo}: ожидалось минимум 2 поля (Name,Type), получено {parts.Count}.")
            End If

            Dim name As String = parts(0)
            Dim typeText As String = parts(1).Trim()
            Dim valueText As String = If(parts.Count >= 3, parts(2), "")

            If String.IsNullOrWhiteSpace(name) Then
                Throw New FormatException($"Строка {lineNo}: пустое имя.")
            End If

            Dim t As StrType
            If Not [Enum].TryParse(typeText, True, t) Then
                Throw New FormatException($"Строка {lineNo}: неизвестный тип ""{typeText}"".")
            End If

            If newIndex.ContainsKey(name) Then
                Throw New InvalidOperationException($"Строка {lineNo}: дубликат ключа ""{name}"".")
            End If

            Dim el As New ListElement()
            el.Name = name
            el.Type = t
            el.Value = ListElement.ParseValue(t, valueText)

            newList.Add(el)
            newIndex(name) = el
        Next

        _list = newList
        _index = newIndex
    End Sub

    ' ---------- ПЕРЕЧИСЛЕНИЕ ----------

    Public Function GetEnumerator() As IEnumerator(Of ListElement) _
        Implements IEnumerable(Of ListElement).GetEnumerator
        Return _list.GetEnumerator()
    End Function

    Private Function GetEnumeratorNonGeneric() As IEnumerator _
        Implements IEnumerable.GetEnumerator
        Return _list.GetEnumerator()
    End Function
End Class