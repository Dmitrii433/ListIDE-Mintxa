Imports System.Windows.Forms

Module Module1

    Sub Main()
        Console.Title = "ListIDE 1.0.1"
        Console.WriteLine()
        Console.WriteLine("Starting UI...")

        ' Запускаем форму
        Application.Run(New Form1)

        ' Этот код выполнится ПОСЛЕ закрытия формы
        Console.WriteLine("UI closed. Press Enter to exit...")
        Console.ReadLine()
    End Sub

End Module