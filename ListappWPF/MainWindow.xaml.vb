Imports System.IO
Imports System.Text
Imports System.Windows
Imports System.Windows.Input
Imports Mintxa

Public Class MainWindow
    Private ReadOnly FilePath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "list.txt")
    Private db As New Minilist()

    Public Sub New()
        InitializeComponent()
        AddHandler Me.Loaded, AddressOf MainWindow_Loaded
    End Sub

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs)
        RichTextBox1.AppendText("=== LISTA Console (WPF 1.0.2) ===" & vbCrLf)
        Console.WriteLine("=== WPF UI запущен ===")
        Console.WriteLine("База: " & FilePath)
        LoadDatabase()
        RefreshListView()
    End Sub

    Private Sub Button1_Click(sender As Object, e As RoutedEventArgs)
        Dim command As String = TextBox1.Text.Trim()
        If String.IsNullOrEmpty(command) Then Return
        RichTextBox1.AppendText("LISTA> " & command & vbCrLf)
        Console.WriteLine("LISTA> " & command)
        TextBox1.Clear()
        Try
            ExecuteCommand(command)
        Catch ex As Exception
            RichTextBox1.AppendText("  Ошибка: " & ex.Message & vbCrLf)
            Console.WriteLine("  Ошибка: " & ex.Message)
        End Try
        RichTextBox1.ScrollToEnd()
    End Sub

    Private Sub TextBox1_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Enter Then Button1_Click(sender, e)
    End Sub

    Private Sub ExecuteCommand(line As String)
        Dim tokens() As String = line.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        If tokens.Length = 0 Then Return
        Select Case tokens(0).ToUpperInvariant()
            Case "NEW" : CmdNew(line, tokens)
            Case "REMOVE" : CmdRemove(tokens)
            Case "OBTAIN" : CmdObtain(tokens)
            Case "HELP" : PrintHelp()
            Case "CLEAR"
                RichTextBox1.Document.Blocks.Clear()
                RichTextBox1.AppendText("=== LISTA Console (WPF 1.0.2) ===" & vbCrLf & vbCrLf)
                Console.Clear()
            Case Else
                RichTextBox1.AppendText($"  Неизвестная команда: ""{tokens(0)}""." & vbCrLf)
        End Select
        RichTextBox1.AppendText(vbCrLf)
    End Sub

    Private Sub CmdNew(line As String, tokens() As String)
        If tokens.Length < 3 Then Return
        Dim typeText As String = tokens(1).ToUpperInvariant()
        Dim name As String = tokens(2)
        Dim prefix As String = $"NEW {tokens(1)} {name} "
        Dim value As String = If(line.Length > prefix.Length, line.Substring(prefix.Length).Trim(), "")
        Dim t As StrType
        Select Case typeText
            Case "STRING" : t = StrType.String
            Case "INTEGER" : t = StrType.Integer
            Case "BINARY" : t = StrType.Binary
            Case Else : Return
        End Select
        If db.Contains(name) Then Return
        Try
            Dim parsed As Object = ListElement.ParseValue(t, value)
            Dim el As New ListElement()
            el.Value = parsed
            db.AddElement(name, t, el)
            SaveDatabase()
            RefreshListView()
            RichTextBox1.AppendText($"  Добавлено: [{db.Count - 1}] {el}" & vbCrLf)
        Catch ex As Exception
            RichTextBox1.AppendText($"  Ошибка: {ex.Message}" & vbCrLf)
        End Try
    End Sub

    Private Sub CmdRemove(tokens() As String)
        If tokens.Length < 3 Then Return
        db.RemoveElement(tokens(2))
        SaveDatabase()
        RefreshListView()
        RichTextBox1.AppendText($"  Удалено: {tokens(2)}" & vbCrLf)
    End Sub

    Private Sub CmdObtain(tokens() As String)
        If db.Count = 0 Then
            RichTextBox1.AppendText("  (база пуста)" & vbCrLf)
        Else
            For i As Integer = 0 To db.Count - 1
                RichTextBox1.AppendText($"  [{i}] Name = ""{db(i).Name}"", Type = {db(i).Type}, Value = {db(i).ValueToString()}" & vbCrLf)
            Next
        End If
    End Sub

    Private Sub PrintHelp()
        RichTextBox1.AppendText("  NEW STRING|INTEGER|BINARY <name> <value>" & vbCrLf)
        RichTextBox1.AppendText("  REMOVE STRING <name>" & vbCrLf)
        RichTextBox1.AppendText("  OBTAIN DATA" & vbCrLf)
    End Sub

    Private Sub Button2_Click(sender As Object, e As RoutedEventArgs)
        Dim name As String = TextBox2.Text.Trim()
        Dim typeStr As String = TextBox3.Text.Trim()
        Dim value As String = RichTextBox3.Text.Trim()
        If String.IsNullOrEmpty(name) OrElse String.IsNullOrEmpty(typeStr) Then
            MessageBox.Show("Введите ключ и тип!")
            Return
        End If
        Dim t As StrType
        Select Case typeStr.ToUpperInvariant()
            Case "1", "STRING" : t = StrType.String
            Case "2", "INTEGER" : t = StrType.Integer
            Case "3", "BINARY" : t = StrType.Binary
            Case Else
                MessageBox.Show("Неверный тип.")
                Return
        End Select
        Try
            Dim parsed As Object = ListElement.ParseValue(t, value)
            Dim el As New ListElement()
            el.Value = parsed
            db.AddElement(name, t, el)
            SaveDatabase()
            RefreshListView()
            MessageBox.Show("Запись добавлена!")
            TextBox2.Clear() : TextBox3.Clear() : RichTextBox3.Clear()
        Catch ex As Exception
            MessageBox.Show("Ошибка: " & ex.Message)
        End Try
    End Sub

    Private Sub RefreshListView()
        ListView1.ItemsSource = db.Select(Function(el) New With {
            .Name = el.Name,
            .Type = el.Type.ToString(),
            .Value = el.ValueToString()
        }).ToList()
    End Sub

    Private Sub SaveDatabase()
        File.WriteAllText(FilePath, db.ToCsv(), New UTF8Encoding(False))
    End Sub

    Private Sub LoadDatabase()
        If File.Exists(FilePath) Then
            db.FromCsv(File.ReadAllText(FilePath, Encoding.UTF8))
        End If
    End Sub
End Class