using System;
using System.Windows.Forms;

namespace Kalendarz1
{
    internal class MojeObliczenia // Zmieniona nazwa klasy na uniknięcie konfliktu
    {
        public void ObliczWageDni(TextBox WagaDni, TextBox RoznicaDni)
        {
            // Sprawdzenie, czy RoznicaDni zawiera wartość
            if (!string.IsNullOrEmpty(RoznicaDni.Text))
            {
                double suma = 0.4 + (Convert.ToDouble(RoznicaDni.Text) * 0.057);
                WagaDni.Text = suma.ToString();
            }
            else
            {
                // Jeśli RoznicaDni jest puste, zostaw WagaDni bez zmian
                WagaDni.Text = string.Empty;
            }
        }
        public int ObliczRozniceDni(DateTimePicker dataWstawienia, TextBox dataDostawy)
        {
            // Deklaracja zmiennych do przechowywania dat i różnicy
            DateTime dataPierwsza;
            DateTime dataDruga;
            int roznicaDni;

            // Sprawdzenie, czy oba teksty reprezentują daty
            if (DateTime.TryParse(dataWstawienia.Text, out dataPierwsza) && DateTime.TryParse(dataDostawy.Text, out dataDruga))
            {
                // Obliczenie różnicy w dniach
                TimeSpan roznica = dataDruga - dataPierwsza;
                roznicaDni = roznica.Days;
            }
            else
            {
                // Ustawienie wartości domyślnej, jeśli daty nie są prawidłowe
                roznicaDni = 0;
            }

            return roznicaDni;
        }
        public void ileSztukOblcizenie(TextBox sztukNaSzuflade, TextBox Wyliczone)
        {
            double WyliczonaSuma;
            double ileautValue;
            double ilesztukValue;

            // Sprawdź, czy zawartość pola 'ileaut' jest liczbą
            if (double.TryParse(sztukNaSzuflade.Text, out ileautValue))
            {
                // Jeśli tak, przekonwertuj zawartość na liczbę
                ilesztukValue = Convert.ToDouble(sztukNaSzuflade.Text);

                // Oblicz wynik
                WyliczonaSuma = ileautValue * 264;
            }
            else
            {
                // Jeśli zawartość pola 'ileaut' nie jest liczbą, ustaw wynik na 0 (lub inną wartość domyślną)
                WyliczonaSuma = 0;
            }

            // Ustaw wynik w odpowiednim polu (np. TextBox)
            // Poniżej zakładam, że wynik ma być ustawiony w polu 'wynikTextBox'
            Wyliczone.Text = WyliczonaSuma.ToString();
        }

        public void ObliczWage(TextBox srednia, TextBox WagaTuszki, TextBox iloscPoj, TextBox sztukNaSzuflade, TextBox Wyliczone, TextBox KGwSkrzynce, TextBox CalcSztukNaSzuflade)
        {
            try
            {
                // Pozostała część Twojej istniejącej logiki...
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void ProponowanaIloscNaSkrzynke(TextBox sztukNaSzuflade, TextBox sztuki, TextBox obliczeniaAut, TextBox srednia, TextBox KGwSkrzynce, TextBox Wyliczone)
        {
            double sredniaValue;
            double KGsztuk;

            // Sprawdź czy wartości są liczbami
            if (double.TryParse(sztuki.Text, out double sztukiValue) && double.TryParse(sztukNaSzuflade.Text, out double sztukNaSzufladeValue))
            {
                // Sprawdź czy wartość sztukNaSzufladeValue jest różna od zera, aby uniknąć dzielenia przez zero
                if (sztukNaSzufladeValue * 264 != 0)
                {
                    double result = sztukiValue / (sztukNaSzufladeValue * 264);
                    obliczeniaAut.Text = result.ToString("0.00");
                }
                else
                {
                    MessageBox.Show("Nie można dzielić przez zero.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }

            // Sprawdź czy wartość sztukNaSzuflade.text jest liczbą
            if (double.TryParse(sztukNaSzuflade.Text, out double sztukNaSzufladeValueParsed))
            {
                string inputValue = srednia.Text.Replace(".", ","); // Zamień kropkę na przecinek, jeśli istnieje

                // Sprawdź, czy inputValue jest liczbą
                if (double.TryParse(inputValue, out sredniaValue))
                {
                    KGsztuk = sredniaValue * sztukNaSzufladeValueParsed;
                    KGwSkrzynce.Text = KGsztuk.ToString("0.00");
                }
            }

            // Wywołaj funkcję ileSztukOblcizenie
            ileSztukOblcizenie(sztukNaSzuflade, Wyliczone);
        }


        public void ObliczenieSztuki(TextBox sztuki, TextBox sztukNaSzuflade, TextBox obliczeniaAut)
        {
            // Sprawdź czy wartości są liczbami
            if (decimal.TryParse(sztuki.Text, out decimal sztukiVal) && decimal.TryParse(sztukNaSzuflade.Text, out decimal sztukNaSzufladeVal))
            {
                // Sprawdź, czy wartość sztukNaSzufladeVal jest różna od zera, aby uniknąć dzielenia przez zero
                if (sztukNaSzufladeVal != 0)
                {
                    decimal result = sztukiVal / (sztukNaSzufladeVal * 264);

                    // Sprawdź, czy wynik jest różny od zera, zanim przypiszesz go do obliczeniaAut.Text
                    if (result != 0)
                    {
                        // Użyj funkcji ToString z formatem, aby ograniczyć wynik do dwóch miejsc po przecinku
                        obliczeniaAut.Text = result.ToString("0.00");
                    }
                }
            }
            else
            {
                MessageBox.Show("Podane wartości muszą być liczbami.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Jeśli wartość w polu sztuki jest pusta, wyjdź z metody
            if (string.IsNullOrEmpty(sztuki.Text))
                return;
        }

    }
}
