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
        //Obliczanie róznicy dni
        public int ObliczRozniceDni(DateTimePicker dataWstawienia, DateTimePicker dataDostawy)
        {
            // Deklaracja zmiennych do przechowywania dat i różnicy
            DateTime dataPierwsza = dataWstawienia.Value;
            DateTime dataDruga = dataDostawy.Value;
            int roznicaDni;

            // Obliczenie różnicy w dniach
            TimeSpan roznica = dataPierwsza - dataDruga;
            roznicaDni = roznica.Days;

            return roznicaDni;
        }

        public int ObliczRozniceDniWstawieniaObecnie(DateTime dataWstawienia, DateTimePicker dataDostawy)
        {
            // Deklaracja zmiennych do przechowywania dat i różnicy
            DateTime dataPierwsza = dataWstawienia;
            DateTime dataDruga = dataDostawy.Value;
            int roznicaDni;

            // Obliczenie różnicy w dniach
            TimeSpan roznica =  dataPierwsza - dataDruga;
            roznicaDni = roznica.Days;

            return roznicaDni;
        }

        //Służy do wyliczenia sztuk *264 i wynik wstawia w textboxie Wyliczone

        public void ZestawDoObliczaniaTransportu(TextBox sztukNaSzuflade, TextBox Wyliczone, TextBox obliczeniaAut, TextBox sztuki, TextBox srednia, TextBox KGwSkrzynce, TextBox obliczeniaAut2, TextBox sztukNaSzuflade2)
        {
            ileSztukOblcizenie(sztukNaSzuflade, Wyliczone);
            ObliczenieAutaCzySieMiesciDlaInnychSztuk(sztukNaSzuflade, sztukNaSzuflade2, sztuki, obliczeniaAut, obliczeniaAut2);
            ObliczenieSztuki(sztuki, sztukNaSzuflade, obliczeniaAut);
            ProponowanaIloscNaSkrzynke(sztukNaSzuflade, srednia, KGwSkrzynce);
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

        /*
        public void ObliczenieAutaCzySieMiesci(TextBox sztukNaSzuflade, TextBox obliczeniaAut)
        {
            // Sprawdź czy wartości są liczbami
            if (double.TryParse(sztukNaSzuflade.Text, out double sztukNaSzufladeValue))
            {
                double sztukiValue = sztukNaSzufladeValue * 264;
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
        }
        */

        public void ObliczenieAutaCzySieMiesciDlaInnychSztuk(TextBox sztukNaSzuflade, TextBox sztukNaSzuflade2, TextBox sztuki,  TextBox obliczeniaAut1, TextBox obliczeniaAut2)
        {
            // Sprawdź czy wartości są liczbami
            if (double.TryParse(sztukNaSzuflade.Text, out double sztukNaSzufladeValue) && double.TryParse(sztukNaSzuflade2.Text, out double sztukNaSzufladeValue2) && double.TryParse(sztuki.Text, out double sztukiVal))
            {
                

                // Sprawdź czy wartość sztukNaSzufladeValue jest różna od zera, aby uniknąć dzielenia przez zero
                if (sztukNaSzufladeValue * 264 != 0)
                {
                    double resultSkrzynki1 = sztukiVal / (sztukNaSzufladeValue * 264);
                    obliczeniaAut1.Text = resultSkrzynki1.ToString("0.00");


                    double Obliczenia1 = (sztukNaSzufladeValue * 264) / (sztukNaSzufladeValue * 264);
                    double Obliczenia2 = (sztukiVal - (sztukNaSzufladeValue * 264)) / (sztukNaSzufladeValue2 * 264);
                    double ObliczeniaSuma = Obliczenia1 + Obliczenia2;
                    obliczeniaAut2.Text = ObliczeniaSuma.ToString("0.00");
                }
                else
                {
                    MessageBox.Show("Nie można dzielić przez zero.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }
        }


        public void ProponowanaIloscNaSkrzynke(TextBox sztukNaSzuflade, TextBox srednia, TextBox KGwSkrzynce)
        {
            double sredniaValue;
            double KGsztuk;

            //ObliczenieAutaCzySieMiesci(sztukNaSzuflade, obliczeniaAut);

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
        }

        
        public static void IleautOblcizenie(TextBox ileaut, TextBox sztuki, TextBox wyliczone)
        {
            double wyliczonaSuma = 0;
            double ileautValue = 0;

            // Sprawdź, czy zawartość pola 'ileaut' jest liczbą
            if (double.TryParse(ileaut.Text, out ileautValue))
            {
                // Jeśli tak, przekonwertuj zawartość na liczbę
                // Oblicz wynik
                wyliczonaSuma = ileautValue * Convert.ToDouble(wyliczone.Text);
            }
            else
            {
                // Jeśli zawartość pola 'ileaut' nie jest liczbą, ustaw wynik na 0 (lub inną wartość domyślną)
                wyliczonaSuma = 0;
            }

            // Ustaw wynik w odpowiednim polu (np. TextBox)
            // Poniżej zakładam, że wynik ma być ustawiony w polu 'sztuki'
            sztuki.Text = wyliczonaSuma.ToString();
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
                
                return;
            }

            // Jeśli wartość w polu sztuki jest pusta, wyjdź z metody
            if (string.IsNullOrEmpty(sztuki.Text))
                return;
        }



    }
}
