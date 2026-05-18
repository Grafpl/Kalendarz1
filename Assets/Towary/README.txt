=== ZDJĘCIA TOWARÓW PRODUKCYJNYCH ===

Wrzucaj zdjęcia w formacie .jpg / .jpeg / .png / .webp z nazwą = kod towaru.

Przykłady:
  Kurczak A.jpg          → towar o kodzie "Kurczak A"
  Kurczak B.png          → towar o kodzie "Kurczak B"
  Filet z piersi.jpg     → towar o kodzie "Filet z piersi"
  Korpus.webp            → towar o kodzie "Korpus"
  Skrzydło.jpg           → towar o kodzie "Skrzydło"
  Wątroba.jpg            → towar o kodzie "Wątroba"
  Żołądki.jpg            → towar o kodzie "Żołądki"
  Serce.jpg              → towar o kodzie "Serce"

Sugerowany rozmiar: 400x300 px (lub większe — będzie automatycznie pomniejszane).

Jeśli zdjęcie nie istnieje, w karcie towaru pokaże się duża ikona kategorii
(🥩 / ❄️ / 🗑 / 🐔) na tle koloru kategorii.

Folder: bin/Debug/net8.0-windows7.0/Assets/Towary/  (po build)
        lub: Assets/Towary/  (w solucji — kopiowane do output)

WAŻNE: Aby pliki były kopiowane do output po build, kliknij prawym
na pliku w Solution Explorer → Properties → Copy to Output Directory:
"Copy if newer". Albo dodaj wpis w .csproj:

  <ItemGroup>
    <None Include="Assets\Towary\*.*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
