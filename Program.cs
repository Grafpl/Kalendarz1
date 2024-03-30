using System;
using System.Windows.Forms;

namespace Kalendarz1
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Utwórz instancję formularza logowania
            LoginForm loginForm = new LoginForm();

            // Wyświetl formularz logowania
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                // Jeśli zalogowano poprawnie, uruchom główne okno
                Application.Run(new WidokKalendarza());
            }
            else
            {
                // W przeciwnym razie, zakończ aplikację
                Application.Exit();
            }
        }
    }
}
