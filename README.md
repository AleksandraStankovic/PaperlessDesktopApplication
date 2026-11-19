# Paperless Desktop

Desktop klijent za [Paperless-ngx](https://docs.paperless-ngx.com/), napravljen u WPF-u kao projekat iz predmeta Interakcija čovjek-računar na ETF-u u Banjoj Luci. Aplikacija se povezuje na Paperless server preko REST API-ja i omogućava rad sa dokumentima bez otvaranja web interfejsa.

## Šta aplikacija radi

- **Dokumenti** – pregled svih dokumenata kao lista ili kao kartice, pretraga po nazivu i sadržaju, filteri po tagovima, korespondentu i tipu dokumenta, i straničenje. Klikom na dokument otvara se pregled sa detaljima, gdje se mogu dodavati i uklanjati tagovi, a fajl se može otvoriti i u podrazumijevanoj aplikaciji. Dokument se može i obrisati.
- **Dodaj dokument** – fajl se prevuče u prozor ili izabere ručno, unese se naziv i po želji korespondent, tip i tagovi. Aplikacija prije slanja provjerava da li dokument već postoji, prikazuje napredak slanja i obrade na serveru, a slanje se može i prekinuti. Podržani su PDF, slike (PNG, JPG, TIFF, GIF, WebP) i TXT, do 50 MB.
- **Tagovi** – kreiranje, uređivanje i brisanje tagova, uz izbor boje i broj dokumenata za svaki tag.
- **Analitika** – osnovna statistika: broj dokumenata, tagova i korespondenata, grafikon po tipu dokumenta, broj dokumenata po mjesecima i najčešći tagovi. Klikom na dio grafikona, mjesec ili tag prelazi se na listu tih dokumenata.
- **Podešavanja** – jezik (srpski ili engleski), svijetla ili tamna tema i boja aplikacije. Promjene se primjenjuju odmah i čuvaju se automatski.

## Tehnologije

- .NET 8, WPF, MVVM ([CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet))
- [Material Design in XAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) za izgled
- WebView2 za pregled dokumenata, SkiaSharp za sličice

## Pokretanje

Potrebni su Windows, .NET 8 SDK i WebView2 Runtime (na Windows 10/11 je uglavnom već instaliran).

1. Adresa Paperless servera i API token su već upisani u `PaperlessDesktop/appsettings.json`, pa aplikacija radi bez dodatnog podešavanja. Za drugi server dovoljno je promijeniti ta dva polja:

   ```json
   {
     "ServerUrl": "http://adresa-servera:8000",
     "ApiToken": "vaš-token"
   }
   ```

   Token se dobija u Paperless web interfejsu, u profilu korisnika.

2. Otvoriti `PaperlessDesktop.sln` u Visual Studiju i pokrenuti projekat, ili iz komandne linije:

   ```
   dotnet run --project PaperlessDesktop
   ```

Korisnička podešavanja se čuvaju u `%AppData%\PaperlessDesktop\settings.json`, a log fajlovi u `%AppData%\PaperlessDesktop\logs`.

## Licenca

MIT, pogledati [LICENSE](LICENSE).
