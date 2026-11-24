# Nasazení jedné veterinární ordinace

Aplikace je ukázkový web a administrace jedné veterinární ordinace.

Backend tvoří jeden ASP.NET Core proces na .NET 10 LTS. Publikovaný React web obsluhuje stejný proces a používá stejnou HTTPS doménu. SQLite je jediné podporované úložiště. Pro tuto velikost ordinace je vhodný modulární monolit; mikroservisy ani další vrstvy pro jednotlivé kliniky nejsou potřeba. Doména obsahuje entity, Application případy užití a porty, Infrastructure SQLite a e-mail, Api HTTP a konfiguraci. `ClinicId` zůstává stabilním klíčem existujících dat, runtime jej vždy nastavuje na jednu ordinaci. Staré migrace se zachovávají jako historie databáze.

## Konfigurace

Výchozí seznam proměnných je v `deploy/production.env.example`. Skutečné hodnoty patří do prostředí služby nebo správce tajemství; `.env` se automaticky nenačítá. Je nutné doplnit HTTPS doménu, SMTP účet, unikátní CSRF secret alespoň 32 znaků a privátní trvalý adresář pro databázi a Data Protection klíče. `AllowedHosts` musí odpovídat hostname `PUBLIC_ORIGIN`. SMTP musí podporovat STARTTLS na nakonfigurovaném portu, typicky 587. Web rezervaci potvrdí až po ověření e-mailu; bez SMTP online rezervace nejsou provozuschopné.

Pro jediný server doporučujeme Caddy na hostiteli a API dostupné pouze na `127.0.0.1:8080`. Ukázka proxy je v `deploy/Caddyfile`. Caddy zajišťuje HTTPS, API povoluje forwarded IP a protokol pouze z explicitních `TRUSTED_PROXIES`. Pokud API běží v Docker bridge síti, místo loopback je nutné nakonfigurovat přesnou adresu skutečného proxy peeru. Nepovolujte všechny sítě ani libovolné forwarded hlavičky. Docker port publikujte pouze na loopback: `-p 127.0.0.1:8080:8080`.

Soubor databáze, jeho journal/WAL a adresář klíčů musí být zapisovatelné jen uživatelem služby. Klíče jsou uložené na disku; jejich ochranu poskytují oprávnění hostitele a šifrování disku/záloh. V kontejneru služba běží pod neprivilegovaným `APP_UID`; připojený svazek musí mít odpovídající vlastnictví. Pro jednu SQLite databázi používejte jeden webový proces na lokálním disku, nikoli síťový sdílený disk nebo více serverů. Databázová transakce chrání také souběžné požadavky mezi procesy, ale limity HTTP jsou lokální procesu.

## Sestavení a inicializace

```sh
npm ci --prefix client
npm run typecheck --prefix client
npm run build --prefix client
dotnet restore api/Veterinar.sln --locked-mode
dotnet test api/Veterinar.sln -c Release
dotnet publish api/src/Veterinar.Api -c Release -o publish
```

Zkopírujte obsah `client/dist` do `publish/wwwroot`. Alternativně `docker build --pull -t veterinar-clinic .` provede sestavení obou částí. CI obsahuje také sestavení kontejneru. Základní image používejte s aktuálními bezpečnostními aktualizacemi a po ověření releasu zaznamenejte jeho digest.

Před každou migrací zastavte webový proces a vytvořte konzistentní zálohu. Na existující databázi nejprve ověřte upgrade na kopii. S nastaveným produkčním prostředím spusťte z publikačního adresáře:

```sh
dotnet Veterinar.Api.dll --initialize-database
```

První inicializace vyžaduje `ADMIN_EMAIL` a unikátní `ADMIN_PASSWORD` alespoň 16 znaků. Existující účet se nepřepisuje. Pak obě bootstrap proměnné odeberte z webové služby a spusťte `dotnet Veterinar.Api.dll`. Produkční start automaticky nemigruje ani nezakládá účet; při chybějící migraci či aktivním vlastníkovi se zastaví.

Migrace přidává indexované UTC časy ke stávajícím rezervacím a ponechává jejich stavy a původní časové okamžiky. Staré rezervace nedostanou expiraci. Nové časy se vytvářejí v `Europe/Prague`, včetně letního času. Pokud byly ve staré verzi zadané místní časy chybně považovány za UTC, je nutné jejich věcnou správnost projít s ordinací; automatické posunutí historických termínů by mohlo změnit skutečně domluvenou návštěvu.

## Provoz

`/api/health` je liveness, `/api/health/ready` ověřuje spojení s databází. Sledujte výskyt 500/503, odmítnutí rezervací a chyby odesílání e-mailu. API používá korelační `X-Request-ID`; loguje šablonu endpointu, status a dobu zpracování bez kontaktů, těla požadavku, hesel nebo potvrzovacích tokenů. Nezapínejte EF sensitive-data logging ani logování SMTP zpráv. V proxy logujte pouze cestu bez query; potvrzovací token je ve fragmentu odkazu a potvrzení se provádí POSTem.

Veřejná rezervace blokuje termín na 15 minut, do potvrzení e-mailem. Po vypršení se přestane započítávat do obsazenosti; záznam se uchovává pro dohledání. Administrátor zadává přímo potvrzenou rezervaci a může ji zrušit v kalendáři. Přihlášení má absolutní osmihodinovou platnost a při změně aktivity, role nebo hesla se zneplatní.

Zálohujte SQLite konzistentním backup mechanismem nebo při zastaveném procesu a zároveň zálohujte chráněné klíče. Otestujte obnovu do odděleného adresáře. Při rollbacku schématu obnovte zálohu společně s předchozím releasem; nepoužívejte automaticky destruktivní downgrade migrace. Retenci kontaktů a nepotvrzených rezervací stanovte s provozovatelem a odstraňování provádějte řízeně ze zálohované databáze.

Před ostrým spuštěním ověřte na cílovém hostiteli HTTPS, skutečný proxy peer, doručení a potvrzení e-mailu, přihlášení, rezervaci, zrušení a obnovu zálohy. Lokální testy nenahrazují tyto kontroly konkrétní domény a SMTP služby.
