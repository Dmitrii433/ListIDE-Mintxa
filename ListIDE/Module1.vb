Imports System.IO
Imports System.Linq
Imports System.Text
Imports Mintxa

Module Module1

    Private Const FilePath As String = "list.txt"
    Private ReadOnly Utf8NoBom As New UTF8Encoding(False)

    Private db As New Minilist()

    Sub Main()
        Console.OutputEncoding = Encoding.UTF8
        LoadFromFile()

        Do
            Console.Clear()
            PrintHeader()
            PrintList()
            PrintMenu()

            Console.Write("Выбор: ")
            Dim input As String = Console.ReadLine()
            If input Is Nothing Then Exit Do
            input = input.Trim()

            Console.Clear()                  ' после выбора в главном меню

            If input.Length = 0 Then Continue Do

            Try
                Select Case input.ToUpperInvariant()
                    Case "1" : AddElement()
                    Case "2" : RemoveElement()
                    Case "3" : RenameElement()
                    Case "4" : ChangeType()
                    Case "5" : SetValue()
                    Case "6" : ClearList()
                    Case "7" : ShowLista()
                    Case "8" : ShowDetails()
                    Case "9" : SaveToFile(True)
                    Case "L" : LoadFromFile(True)
                    Case "T" : RunTransactionDemo()
                    Case "0" : ExitApp()
                    Case Else
                        Pause("Неизвестная команда. Введите 0–9, L или T.")
                End Select
            Catch ex As Exception
                Pause($"Ошибка: {ex.Message}")
            End Try
        Loop
    End Sub

    ' ================== МЕНЮ ==================

    Private Sub PrintHeader()
        Console.WriteLine("========================================")
        Console.WriteLine("        ListIDE — мини-БД Minilist")
        Console.WriteLine($"        Файл: {Path.GetFullPath(FilePath)}")
        If db.InTransaction Then
            Console.WriteLine("        [ТРАНЗАКЦИЯ ОТКРЫТА]")
        End If
        Console.WriteLine("========================================")
    End Sub

    Private Sub PrintList()
        Console.WriteLine()
        Console.WriteLine($"Элементов: {db.Count}")
        Console.WriteLine("----------------------------------------")

        If db.Count = 0 Then
            Console.WriteLine("  (база пуста)")
        Else
            Dim i As Integer = 0
            For Each el As ListElement In db
                Console.WriteLine($"  [{i}] {el}")
                i += 1
            Next
        End If

        Console.WriteLine("----------------------------------------")
    End Sub

    Private Sub PrintMenu()
        Console.WriteLine()
        Console.WriteLine("  1. Добавить запись")
        Console.WriteLine("  2. Удалить по ключу")
        Console.WriteLine("  3. Переименовать ключ")
        Console.WriteLine("  4. Изменить тип")
        Console.WriteLine("  5. Изменить значение")
        Console.WriteLine("  6. Очистить базу")
        Console.WriteLine("  7. LISTA-запросы")
        Console.WriteLine("  8. Подробности")
        Console.WriteLine("  9. Сохранить в list.txt")
        Console.WriteLine("  L. Загрузить из list.txt")
        Console.WriteLine("  T. Демо транзакции")
        Console.WriteLine("  0. Выход (с сохранением)")
        Console.WriteLine()
    End Sub

    ' ================== LISTA-КОНСОЛЬ (пункт 7) ==================

    Private Sub ShowLista()
        Console.Clear()
        PrintListaHeader()

        Do
            Console.Write("LISTA> ")
            Dim line As String = Console.ReadLine()
            If line Is Nothing Then
                Console.Clear()
                Return
            End If
            line = line.Trim()
            If line.Length = 0 Then Continue Do

            Dim cmd As String = line.ToUpperInvariant()

            If cmd = "EXIT" Then
                Console.Clear()
                Return
            End If

            If cmd = "CLEAR" Then
                Console.Clear()
                PrintListaHeader()
                Continue Do
            End If

            Try
                ExecuteCommand(line)
            Catch ex As Exception
                Console.WriteLine($"  Ошибка: {ex.Message}")
            End Try

            Console.WriteLine()
        Loop
    End Sub

    Private Sub PrintListaHeader()
        Console.WriteLine("========================================")
        Console.WriteLine("        LISTA-консоль")
        Console.WriteLine("========================================")
        Console.WriteLine("  NEW STRING  <name> <value>")
        Console.WriteLine("  NEW INTEGER <name> <value>")
        Console.WriteLine("  NEW BINARY  <name> <hex>")
        Console.WriteLine("  REMOVE STRING <name>")
        Console.WriteLine("  OBTAIN DATA")
        Console.WriteLine("  HELP")
        Console.WriteLine("  CLEAR")
        Console.WriteLine("  EXIT")
        Console.WriteLine()
    End Sub

    ' ================== КОМАНДНЫЙ РЕЖИМ ==================

    Private Sub ExecuteCommand(line As String)
        Dim tokens() As String = line.Split(New Char() {" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
        If tokens.Length = 0 Then Return

        Dim cmd As String = tokens(0).ToUpperInvariant()

        Select Case cmd
            Case "NEW" : CmdNew(line, tokens)
            Case "REMOVE" : CmdRemove(tokens)
            Case "OBTAIN" : CmdObtain(tokens)
            Case "HELP" : PrintHelp()
            Case Else
                Console.WriteLine($"  Неизвестная команда: ""{cmd}"". Введите HELP.")
        End Select
    End Sub

    Private Sub CmdNew(line As String, tokens() As String)
        If tokens.Length < 3 Then
            Console.WriteLine("  Использование: NEW STRING|INTEGER|BINARY <name> <value>")
            Return
        End If

        Dim typeText As String = tokens(1).ToUpperInvariant()
        Dim name As String = tokens(2)
        Dim prefix As String = $"NEW {tokens(1)} {name} "
        Dim value As String = If(line.Length > prefix.Length, line.Substring(prefix.Length).Trim(), "")

        Dim t As StrType
        Select Case typeText
            Case "STRING" : t = StrType.String
            Case "INTEGER" : t = StrType.Integer
            Case "BINARY" : t = StrType.Binary
            Case Else
                Console.WriteLine($"  Неизвестный тип: ""{tokens(1)}"". Используйте STRING, INTEGER или BINARY.")
                Return
        End Select

        If db.Contains(name) Then
            Console.WriteLine($"  Ключ уже существует: {name}")
            Return
        End If

        Dim parsed As Object
        Try
            parsed = ListElement.ParseValue(t, value)
        Catch ex As FormatException
            Console.WriteLine($"  Неверное значение для {t}: {ex.Message}")
            Return
        End Try

        Dim el As New ListElement()
        el.Value = parsed
        db.AddElement(name, t, el)
        SaveToFile(False)
        Console.WriteLine($"  Добавлено: [{db.Count - 1}] {el}")
    End Sub

    Private Sub CmdRemove(tokens() As String)
        If tokens.Length < 3 Then
            Console.WriteLine("  Использование: REMOVE STRING <name>")
            Return
        End If

        If tokens(1).ToUpperInvariant() <> "STRING" Then
            Console.WriteLine("  Поддерживается только: REMOVE STRING <name>")
            Return
        End If

        Dim name As String = tokens(2)

        If Not db.Contains(name) Then
            Console.WriteLine($"  Не найдено: {name}")
            Return
        End If

        db.RemoveElement(name)
        SaveToFile(False)
        Console.WriteLine($"  Удалено: {name}")
    End Sub

    Private Sub CmdObtain(tokens() As String)
        If tokens.Length <> 2 OrElse tokens(1).ToUpperInvariant() <> "DATA" Then
            Console.WriteLine("  Использование: OBTAIN DATA (без параметров)")
            Return
        End If

        Console.WriteLine("  ----------------------------------------")
        If db.Count = 0 Then
            Console.WriteLine("    (база пуста)")
        Else
            Dim i As Integer = 0
            For Each el As ListElement In db
                Console.WriteLine($"    [{i}] Name = ""{el.Name}"", Type = {el.Type}, Value = {el.ValueToString()}")
                i += 1
            Next
        End If
        Console.WriteLine("  ----------------------------------------")
    End Sub

    Private Sub PrintHelp()
        Console.WriteLine("  NEW STRING  <name> <value>")
        Console.WriteLine("  NEW INTEGER <name> <value>")
        Console.WriteLine("  NEW BINARY  <name> <hex>")
        Console.WriteLine("  REMOVE STRING <name>")
        Console.WriteLine("  OBTAIN DATA")
        Console.WriteLine("  HELP")
        Console.WriteLine("  CLEAR")
        Console.WriteLine("  EXIT")
    End Sub

    ' ================== CRUD (МЕНЮ) ==================

    Private Sub AddElement()
        Console.Clear()
        Console.WriteLine("--- Добавление ---")
        Dim name As String = ReadNonEmpty("Ключ (Name): ")
        If db.Contains(name) Then
            Pause($"Ключ уже существует: {name}")
            Return
        End If
        Dim type As StrType = ReadType()
        Dim value As Object = ReadValue(type)
        Dim el As New ListElement()
        el.Value = value
        db.AddElement(name, type, el)
        SaveToFile(False)
        Pause($"Добавлено: {el}")
    End Sub

    Private Sub RemoveElement()
        Console.Clear()
        Console.WriteLine("--- Удаление ---")
        If db.Count = 0 Then
            Pause("База пуста.")
            Return
        End If
        Dim name As String = ReadNonEmpty("Ключ для удаления: ")
        If db.Contains(name) Then
            db.RemoveElement(name)
            SaveToFile(False)
            Pause($"Удалено: {name}")
        Else
            Pause($"Не найдено: {name}")
        End If
    End Sub

    Private Sub RenameElement()
        Console.Clear()
        Console.WriteLine("--- Переименование ключа ---")
        If db.Count = 0 Then
            Pause("База пуста.")
            Return
        End If
        Dim oldName As String = ReadNonEmpty("Старый ключ: ")
        If Not db.Contains(oldName) Then
            Pause($"Не найдено: {oldName}")
            Return
        End If
        Dim newName As String = ReadNonEmpty("Новый ключ: ")
        db.Rename(oldName, newName)
        SaveToFile(False)
        Pause($"Переименовано: {oldName} -> {newName}")
    End Sub

    Private Sub ChangeType()
        Console.Clear()
        Console.WriteLine("--- Изменение типа ---")
        If db.Count = 0 Then
            Pause("База пуста.")
            Return
        End If
        Dim name As String = ReadNonEmpty("Ключ: ")
        If Not db.Contains(name) Then
            Pause($"Не найдено: {name}")
            Return
        End If
        Dim newType As StrType = ReadType()
        db.ChangeType(name, newType)
        SaveToFile(False)
        Pause($"Тип изменён: {db.GetByName(name)}")
    End Sub

    Private Sub SetValue()
        Console.Clear()
        Console.WriteLine("--- Изменение значения ---")
        If db.Count = 0 Then
            Pause("База пуста.")
            Return
        End If
        Dim name As String = ReadNonEmpty("Ключ: ")
        Dim el As ListElement = db.GetByName(name)
        If el Is Nothing Then
            Pause($"Не найдено: {name}")
            Return
        End If
        Dim newValue As Object = ReadValue(el.Type)
        db.UpdateValue(name, newValue)
        SaveToFile(False)
        Pause($"Значение изменено: {el}")
    End Sub

    Private Sub ClearList()
        Console.Clear()
        Console.WriteLine("--- Очистка ---")
        If db.Count = 0 Then
            Pause("База уже пуста.")
            Return
        End If
        Console.Write($"Удалить все {db.Count} записей? (y/n): ")
        If Console.ReadLine().Trim().ToLower() = "y" Then
            db.Clear()
            SaveToFile(False)
            Pause("База очищена.")
        Else
            Pause("Отменено.")
        End If
    End Sub

    Private Sub ShowDetails()
        Console.Clear()
        Console.WriteLine("--- Подробности ---")
        If db.Count = 0 Then
            Pause("База пуста.")
            Return
        End If
        Dim i As Integer = 0
        For Each el As ListElement In db
            Console.WriteLine($"  [{i}] Name = ""{el.Name}"", Type = {el.Type}, Value = {el.ValueToString()}")
            i += 1
        Next
        Pause()
    End Sub

    Private Sub RunTransactionDemo()
        Console.Clear()
        Console.WriteLine("--- Демо транзакции ---")
        If db.InTransaction Then
            Pause("Транзакция уже открыта.")
            Return
        End If
        Dim testKey As String = "__tx_demo__"
        If db.Contains(testKey) Then db.RemoveElement(testKey)
        Console.WriteLine("BeginTransaction: добавляем временную запись...")
        db.BeginTransaction()
        Try
            Dim el As New ListElement()
            el.Value = "temp"
            db.AddElement(testKey, StrType.String, el)
            Console.WriteLine($"  Добавлено: {db.GetByName(testKey)}")
            Console.Write("Закоммитить транзакцию? (y = commit, n = rollback): ")
            Dim ans As String = Console.ReadLine().Trim().ToLower()
            If ans = "y" Then
                db.Commit()
                Console.WriteLine("  Commit: временная запись сохранена.")
            Else
                db.Rollback()
                Console.WriteLine("  Rollback: временная запись откатана.")
            End If
        Catch ex As Exception
            db.Rollback()
            Console.WriteLine($"  Исключение: {ex.Message}. Сделан Rollback.")
        End Try
        SaveToFile(False)
        Pause("Готово.")
    End Sub

    Private Sub SaveToFile(Optional showMessage As Boolean = False)
        Try
            Dim text As String = db.ToCsv()
            Dim tmp As String = FilePath & ".tmp"
            File.WriteAllText(tmp, text, Utf8NoBom)
            If File.Exists(FilePath) Then
                File.Replace(tmp, FilePath, Nothing)
            Else
                File.Move(tmp, FilePath)
            End If
            If showMessage Then
                Console.Clear()
                Console.WriteLine("--- Сохранение ---")
                Pause($"Сохранено {db.Count} записей в {Path.GetFullPath(FilePath)}")
            End If
        Catch ex As Exception
            Pause($"Ошибка сохранения: {ex.Message}")
        End Try
    End Sub

    Private Sub LoadFromFile(Optional showMessage As Boolean = False)
        Try
            If Not File.Exists(FilePath) Then
                If showMessage Then
                    Console.Clear()
                    Console.WriteLine("--- Загрузка ---")
                    Pause($"Файл не найден: {Path.GetFullPath(FilePath)}")
                End If
                Return
            End If
            Dim text As String = File.ReadAllText(FilePath, Encoding.UTF8)
            db.FromCsv(text)
            If showMessage Then
                Console.Clear()
                Console.WriteLine("--- Загрузка ---")
                Pause($"Загружено {db.Count} записей из {Path.GetFullPath(FilePath)}")
            End If
        Catch ex As Exception
            Pause($"Ошибка загрузки: {ex.Message}")
        End Try
    End Sub

    Private Sub ExitApp()
        SaveToFile(False)
        Console.WriteLine("Сохранено. Выход...")
        Environment.Exit(0)
    End Sub

    Private Function ReadNonEmpty(prompt As String) As String
        Do
            Console.Write(prompt)
            Dim s As String = Console.ReadLine()
            If Not String.IsNullOrWhiteSpace(s) Then Return s.Trim()
            Console.WriteLine("  Пустое значение не допускается.")
        Loop
    End Function

    Private Function ReadType() As StrType
        Do
            Console.WriteLine("  Типы: 1=String, 2=Integer, 3=Binary")
            Console.Write("  Тип: ")
            Select Case Console.ReadLine().Trim()
                Case "1" : Return StrType.String
                Case "2" : Return StrType.Integer
                Case "3" : Return StrType.Binary
                Case Else
                    Console.WriteLine("  Неверный выбор, повторите.")
            End Select
        Loop
    End Function

    Private Function ReadValue(type As StrType) As Object
        Do
            Select Case type
                Case StrType.String
                    Console.Write("  Значение (строка): ")
                    Return Console.ReadLine()
                Case StrType.Integer
                    Console.Write("  Значение (целое): ")
                    Dim s As String = Console.ReadLine()
                    Dim n As Integer
                    If Integer.TryParse(s, n) Then Return n
                    Console.WriteLine("  Не целое число, повторите.")
                Case StrType.Binary
                    Console.Write("  Значение (hex, напр. 0A1B): ")
                    Dim s As String = Console.ReadLine().Trim()
                    If s.Length Mod 2 = 0 AndAlso s.All(Function(c) Uri.IsHexDigit(c)) Then
                        Dim bytes(s.Length \ 2 - 1) As Byte
                        For i As Integer = 0 To bytes.Length - 1
                            bytes(i) = Convert.ToByte(s.Substring(i * 2, 2), 16)
                        Next
                        Return bytes
                    End If
                    Console.WriteLine("  Неверный hex, повторите.")
            End Select
        Loop
    End Function

    Private Sub Pause(Optional message As String = "")
        If Not String.IsNullOrEmpty(message) Then
            Console.WriteLine()
            Console.WriteLine(message)
        End If
        Console.WriteLine()
        Console.Write("Нажмите Enter...")
        Console.ReadLine()
    End Sub

End Module