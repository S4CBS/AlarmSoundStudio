// Минимальное приложение для теста активации MSIX
using System;
using System.Windows.Forms;

namespace TestMin
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            MessageBox.Show("Тестовый пакет АКТИВИРОВАЛСЯ УСПЕШНО!", "TestMin",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
