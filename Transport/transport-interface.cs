// Plik: /Pakowanie/IPakowanieSerwis.cs
// Interfejs serwisu pakowania

using System.Collections.Generic;

namespace Kalendarz1.Transport.Pakowanie
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za obliczenia pakowania ładunków
    /// </summary>
    public interface IPakowanieSerwis
    {
        /// <summary>
        /// Oblicza wypełnienie kursu dla podanych ładunków
        /// </summary>
        /// <param name="ladunki">Lista ładunków do spakowania</param>
        /// <param name="paletyPojazdu">Pojemność pojazdu w paletach H1</param>
        /// <param name="planE2NaPalete">Plan pojemników na paletę (36 lub 40)</param>
        /// <returns>Wynik pakowania z obliczonymi wartościami</returns>
        WynikPakowania ObliczKurs(IList<PozycjaLike> ladunki, int paletyPojazdu, int planE2NaPalete = 36);

        /// <summary>
        /// Pakuje ładunki zachłannie, zwracając listę zapakowanych i nadwyżkę
        /// </summary>
        /// <param name="ladunki">Lista ładunków do spakowania</param>
        /// <param name="paletyPojazdu">Pojemność pojazdu w paletach H1</param>
        /// <param name="planE2NaPalete">Plan pojemników na paletę (36 lub 40)</param>
        /// <returns>Krotka z zapakowymi ładunkami i nadwyżką</returns>
        (IList<PozycjaLike> zapakowane, IList<PozycjaLike> nadwyzka) PakujZachlannie(
            IList<PozycjaLike> ladunki, 
            int paletyPojazdu, 
            int planE2NaPalete = 36);

        /// <summary>
        /// Sprawdza czy dany ładunek zmieści się w pozostałej przestrzeni
        /// </summary>
        /// <param name="ladunek">Ładunek do sprawdzenia</param>
        /// <param name="pozostalePalety">Pozostała liczba palet</param>
        /// <param name="planE2NaPalete">Plan pojemników na paletę</param>
        /// <returns>True jeśli się zmieści</returns>
        bool CzyZmiesciSie(PozycjaLike ladunek, int pozostalePalety, int planE2NaPalete = 36);

        /// <summary>
        /// Optymalizuje kolejność ładunków dla lepszego upakowania
        /// </summary>
        /// <param name="ladunki">Lista ładunków do optymalizacji</param>
        /// <param name="paletyPojazdu">Pojemność pojazdu</param>
        /// <param name="planE2NaPalete">Plan pojemników na paletę</param>
        /// <returns>Zoptymalizowana lista ładunków</returns>
        IList<PozycjaLike> OptymalizujKolejnosc(
            IList<PozycjaLike> ladunki, 
            int paletyPojazdu, 
            int planE2NaPalete = 36);
    }
}