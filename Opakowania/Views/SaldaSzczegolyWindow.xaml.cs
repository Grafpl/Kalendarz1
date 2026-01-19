using System;
using System.Windows;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class SaldaSzczegolyWindow : Window
    {
        public SaldaSzczegolyWindow(SaldoKontrahenta kontrahent, DateTime dataDo, string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            var viewModel = new SaldaSzczegolyViewModel(kontrahent, dataDo, userId);
            viewModel.CloseRequested += () => Close();
            DataContext = viewModel;
        }
    }
}
