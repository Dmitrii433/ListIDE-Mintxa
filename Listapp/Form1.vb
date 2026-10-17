Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms ' <-- Добавлено для Application, MessageBox, ListViewItem
Imports Mintxa

Public Class Form1

    Private ReadOnly FilePath As String = Path.Combine(Application.StartupPath, "list.txt")
    Private db As New Minilist()

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Настройка ListView
        ListView1.View = View.Details
        ListView1.FullRowSelect = True
        ListView1.GridLines = True
        ListView1.Columns.Add("Ключ (Name)", 200)
        ListView1.Columns.Add("Тип (Type)", 100)
        ListView1.Columns.Add("Значение (Value)", 400)

        ' Вывод в RichTextBox
        RichTextBox1.AppendText("=== LISTA Console ===" & vbCrLf)

        ' ВЫВОД В КОНСОЛЬ
        Console.WriteLine("=== ListIDE запущен ===")
        Console.WriteLine("Форма загружена. База данных: " & FilePath)

        LoadDatabase()
        RefreshListView()

        Console.WriteLine($"База загружена. Элементов: {db.Count}")
    End Sub

    ' ================== ВКЛАДКА LISTA ==================

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim command As String = TextBox1.Text.Trim()
        If String.IsNullOrEmpty(command) Then Return

        ' Вывод в форму и в консоль
        RichTextBox1.AppendText("LISTA> " & command & vbCrLf)
        Console.WriteLine("LISTA> " & command)

        TextBox1.Clear()

        Try
            ExecuteCommand(command)
        Catch ex As Exception
            RichTextBox1.AppendText("  Ошибка: " & ex.Message & vbCrLf)
            Console.WriteLine("  Ошибка: " & ex.Message)
        End Try

        RichTextBox1.SelectionStart = RichTextBox1.Text.Length
        RichTextBox1.ScrollToCaret()
    End Sub

    Private Sub ExecuteCommand(line As String)
        Dim tokens() As String = line.Split(New Char() {" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
        If tokens.Length = 0 Then Return

        Dim cmd As String = tokens(0).ToUpperInvariant()

        Select Case cmd
            Case "NEW" : CmdNew(line, tokens)
            Case "REMOVE" : CmdRemove(tokens)
            Case "OBTAIN" : CmdObtain(tokens)
            Case "HELP" : PrintHelp()
            Case "CLEAR"
                RichTextBox1.Clear()
                RichTextBox1.AppendText("=== LISTA Console ===" & vbCrLf & vbCrLf)
                Console.Clear()
            Case Else
                Dim msg As String = $"  Неизвестная команда: ""{cmd}"". Введите HELP."
                RichTextBox1.AppendText(msg & vbCrLf)
                Console.WriteLine(msg)
        End Select
        RichTextBox1.AppendText(vbCrLf)
    End Sub

    Private Sub CmdNew(line As String, tokens() As String)
        If tokens.Length < 3 Then
            RichTextBox1.AppendText("  Использование: NEW STRING|INTEGER|BINARY <name> <value>" & vbCrLf)
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
                RichTextBox1.AppendText($"  Неизвестный тип: ""{tokens(1)}""." & vbCrLf)
                Console.WriteLine($"  Неизвестный тип: ""{tokens(1)}"".")
                Return
        End Select

        If db.Contains(name) Then
            RichTextBox1.AppendText($"  Ключ уже существует: {name}" & vbCrLf)
            Console.WriteLine($"  Ключ уже существует: {name}")
            Return
        End If

        Try
            Dim parsed As Object = ListElement.ParseValue(t, value)
            Dim el As New ListElement()
            el.Value = parsed
            db.AddElement(name, t, el)
            SaveDatabase()
            RefreshListView()

            Dim msg As String = $"  Добавлено: [{db.Count - 1}] {el}"
            RichTextBox1.AppendText(msg & vbCrLf)
            Console.WriteLine(msg)

        Catch ex As Exception
            RichTextBox1.AppendText($"  Ошибка: {ex.Message}" & vbCrLf)
            Console.WriteLine($"  Ошибка: {ex.Message}")
        End Try
    End Sub

    Private Sub CmdRemove(tokens() As String)
        If tokens.Length < 3 Then
            RichTextBox1.AppendText("  Использование: REMOVE STRING <name>" & vbCrLf)
            Console.WriteLine("  Использование: REMOVE STRING <name>")
            Return
        End If

        Dim name As String = tokens(2)
        If Not db.Contains(name) Then
            RichTextBox1.AppendText($"  Не найдено: {name}" & vbCrLf)
            Console.WriteLine($"  Не найдено: {name}")
            Return
        End If

        db.RemoveElement(name)
        SaveDatabase()
        RefreshListView()

        Dim msg As String = $"  Удалено: {name}"
        RichTextBox1.AppendText(msg & vbCrLf)
        Console.WriteLine(msg)
    End Sub

    Private Sub CmdObtain(tokens() As String)
        If tokens.Length <> 2 OrElse tokens(1).ToUpperInvariant() <> "DATA" Then
            RichTextBox1.AppendText("  Использование: OBTAIN DATA" & vbCrLf)
            Console.WriteLine("  Использование: OBTAIN DATA")
            Return
        End If

        If db.Count = 0 Then
            RichTextBox1.AppendText("  (база пуста)" & vbCrLf)
            Console.WriteLine("  (база пуста)")
        Else
            Dim i As Integer = 0
            For Each el As ListElement In db
                Dim msg As String = $"  [{i}] Name = ""{el.Name}"", Type = {el.Type}, Value = {el.ValueToString()}"
                RichTextBox1.AppendText(msg & vbCrLf)
                Console.WriteLine(msg)
                i += 1
            Next
        End If
    End Sub

    Private Sub PrintHelp()
        Dim helpText As String = "  NEW STRING  <name> <value>" & vbCrLf &
                                 "  NEW INTEGER <name> <value>" & vbCrLf &
                                 "  NEW BINARY  <name> <hex>" & vbCrLf &
                                 "  REMOVE STRING <name>" & vbCrLf &
                                 "  OBTAIN DATA" & vbCrLf &
                                 "  CLEAR"
        RichTextBox1.AppendText(helpText & vbCrLf)
        Console.WriteLine(helpText)
    End Sub

    ' ================== ВКЛАДКА INPUT ==================

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        ' ИСПРАВЛЕНО: TextBox2 -> TextBox4, TextBox3 -> TextBox5
        Dim name As String = TextBox4.Text.Trim()
        Dim typeStr As String = TextBox5.Text.Trim()
        Dim value As String = RichTextBox3.Text.Trim()

        If String.IsNullOrEmpty(name) OrElse String.IsNullOrEmpty(typeStr) Then
            MessageBox.Show("Введите ключ и тип!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim t As StrType
        Select Case typeStr.ToUpperInvariant()
            Case "1", "STRING" : t = StrType.String
            Case "2", "INTEGER" : t = StrType.Integer
            Case "3", "BINARY" : t = StrType.Binary
            Case Else
                MessageBox.Show("Неверный тип. Введите 1, 2, 3.", "Ошибка")
                Return
        End Select

        Try
            Dim parsed As Object = ListElement.ParseValue(t, value)
            Dim el As New ListElement()
            el.Value = parsed

            db.AddElement(name, t, el)
            SaveDatabase()
            RefreshListView()

            MessageBox.Show("Запись добавлена!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Console.WriteLine($"Добавлено через форму: {name} ({t}) = {value}")

            ' ИСПРАВЛЕНО: Очистка новых полей
            TextBox4.Clear()
            TextBox5.Clear()
            RichTextBox3.Clear()

        Catch ex As Exception
            MessageBox.Show("Ошибка: " & ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ================== ВСПОМОГАТЕЛЬНЫЕ ==================

    Private Sub RefreshListView()
        ListView1.BeginUpdate()
        ListView1.Items.Clear()
        For Each el As ListElement In db
            ' Вариант 1: Явное приведение к строке
            Dim item As New ListViewItem(CStr(el.Name))
            item.SubItems.Add(el.Type.ToString())
            item.SubItems.Add(el.ValueToString())
            ListView1.Items.Add(item)
        Next
        ListView1.EndUpdate()
    End Sub

    Private Sub SaveDatabase()
        Try
            File.WriteAllText(FilePath, db.ToCsv(), New UTF8Encoding(False))
        Catch ex As Exception
            Console.WriteLine("Ошибка сохранения: " & ex.Message)
        End Try
    End Sub

    Private Sub LoadDatabase()
        Try
            If File.Exists(FilePath) Then
                Dim text As String = File.ReadAllText(FilePath, Encoding.UTF8)
                db.FromCsv(text)
            End If
        Catch ex As Exception
            Console.WriteLine("Ошибка загрузки: " & ex.Message)
        End Try
    End Sub

End Class