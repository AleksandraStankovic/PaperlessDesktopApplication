# Paperless Desktop

Desktop klijent za [Paperless-ngx](https://docs.paperless-ngx.com/), napravljen u WPF-u kao projekat iz predmeta Interakcija čovjek-računar (ETF Banja Luka). Aplikacija se preko REST API-ja povezuje na Paperless server i omogućava rad sa dokumentima bez web interfejsa.

## Mogućnosti

- **Dokumenti** – lista ili kartice, pretraga, filteri (tagovi, korespondent, tip), pregled dokumenta, izmjena tagova i brisanje.
- **Dodaj dokument** – prevlačenje ili izbor fajla, provjera duplikata i prikaz napretka slanja (PDF, slike, TXT, do 50 MB).
- **Tagovi** – kreiranje, uređivanje i brisanje tagova, uz izbor boje.
- **Analitika** – statistika i grafikoni po tipu, mjesecima i tagovima.
- **Podešavanja** – srpski ili engleski jezik, svijetla ili tamna tema, boja aplikacije.

## Tehnologije

.NET 8, WPF, MVVM (CommunityToolkit.Mvvm), Material Design in XAML, WebView2, SkiaSharp.

## Pokretanje

Potrebni su Windows 10/11, .NET 8 SDK i WebView2 Runtime (obično već instaliran).

Adresa servera i API token su već upisani u `PaperlessDesktop/appsettings.json`, pa nije potrebno dodatno podešavanje. Projekat se pokreće iz Visual Studija (`PaperlessDesktop.sln`) ili iz komandne linije:

```
dotnet run --project PaperlessDesktop
```

## Licenca

MIT, pogledati [LICENSE](LICENSE).
