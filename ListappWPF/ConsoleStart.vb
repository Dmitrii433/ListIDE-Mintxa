Imports System
Imports System.Windows

Module ConsoleStart
    <STAThread()>
    Sub Main()
        Console.Title = "ListIDE 1.0.2"
        Console.WriteLine("Загрузка ListIDE (WPF)...")
        Console.WriteLine("Пожалуйста, подождите, WPF инициализируется...")

        Dim app As New System.Windows.Application()
        Dim mainWin As New MainWindow()

        Console.WriteLine("Окно готово. Открываем...")
        app.Run(mainWin)
    End Sub
End Module