# Veterinární ordinace Demo

Ukázkový full-stack projekt, který předvádí web veterinární ordinace, online objednávání a správu obsahu. Ordinace, kontakty a rezervace v demu jsou fiktivní.

## Co aplikace umí

- Návštěvníci najdou služby, ordinační hodiny, kontakty a články.
- Klienti mohou najít nejbližší volný termín a potvrdit rezervaci e-mailem. Soukromý odkaz umožňuje termín přesunout nebo zrušit a potvrzenou návštěvu lze přidat do kalendáře.
- Personál spravuje rezervace, ručně objednává pacienty a upravuje ordinační hodiny, výjimky, služby a obsah webu v administraci.

Podrobnosti o rezervačním procesu popisuje [dokumentace rezervací](docs/BOOKING_EXPERIENCE.md).

## Rychlé spuštění

Potřebujete Git, Node.js **22.12+** s npm a **.NET SDK 10**. V terminálu spusťte:

```sh
git clone https://github.com/Edie17/veterinar-web-cms.git
cd veterinar-web-cms
npm run dev
```

Při prvním spuštění skript nainstaluje klientské závislosti, připraví demo databázi a spustí ASP.NET Core i Vite. Po vypsání adres otevřete:

| Část | Adresa |
| --- | --- |
| Veřejný web | [localhost:5173](http://localhost:5173/) |
| Administrace | [localhost:5173/sprava](http://localhost:5173/sprava) |
| Testovací e-maily | [localhost:5173/api/dev/mailbox/view](http://localhost:5173/api/dev/mailbox/view) |
| Přímá kontrola API | [127.0.0.1:5067/api/health](http://127.0.0.1:5067/api/health) |

Do demo administrace se přihlaste jako `admin@example.cz` s heslem `ChangeMe123!`. Pro vyzkoušení rezervace vyberte budoucí pracovní den a potvrzovací odkaz otevřete z testovací schránky. Demo ukládá služby, ordinační dobu, ukázkový obsah a syntetické rezervace do `.local/veterina-demo.db`. Schránka je v paměti a skutečné e-maily se neposílají. Aplikaci zastavíte stisknutím **Ctrl+C** v terminálu.

Pokud start hlásí obsazený port, uvolněte port **5173** pro frontend nebo **5067** pro API a příkaz zopakujte. Pro samostatný frontend použijte `npm run dev:client --prefix client` a spusťte API na portu 5067.

## Technologie a struktura

| Cesta | Obsah |
| --- | --- |
| [client/](client/) | React, TypeScript a Vite. Veřejný web, administrace a sdílené komponenty. |
| [api/src/](api/src/) | ASP.NET Core na .NET 10, aplikační logika, Entity Framework Core, SQLite a SMTP. |
| [api/tests/](api/tests/) | Doménové, aplikační, integrační a prohlížečové testy. |
| [scripts/](scripts/) | Lokální spuštění a automatizované kontroly. |
| [deploy/](deploy/) | Vzor produkční konfigurace a konfigurace Caddy. |

Aplikace je navržená pro jednu ordinaci.

## Testy a sestavení

```sh
npm ci --prefix client
npm test
npm run typecheck --prefix client
npm run build --prefix client
```

Kompletní kontrola `npm run qa` přidá prohlížečové testy v Chrome nad izolovanou demo databází. Ověřuje veřejný web, průběh rezervace i běžné operace v administraci a ukládá reporty a snímky do `.qa/`. Stejný postup spouští GitHub Actions, který také sestavuje produkční kontejner. [Podrobnosti k QA](docs/QA.md) · [Workflow CI](.github/workflows/ci.yml)

## Konfigurace a produkční nasazení

Lokální konfiguraci mimo demo popisuje [vzor proměnných API](api/src/Veterinar.Api/.env.example). Proměnné nastavte v prostředí procesu, protože aplikace soubory `.env` automaticky nenačítá.

Produkční aplikace obsluhuje frontend i API na jedné HTTPS doméně. Obraz sestavíte příkazem `docker build -t veterinar-clinic .` z kořene repozitáře. Pro nasazení připravte HTTPS doménu, SMTP, trvalé úložiště pro SQLite a klíče Data Protection. Databázi inicializujte před prvním spuštěním podle návodu. [Produkční návod](docs/PRODUCTION.md) · [Vzor produkčních proměnných](deploy/production.env.example)

## Hlášení chyb

Při nahlášení chyby uveďte verzi projektu, kroky k zopakování a očekávané chování. Před vložením logů odstraňte údaje klientů, přihlašovací údaje a rezervační odkazy. [GitHub Issues](https://github.com/Edie17/veterinar-web-cms/issues)

## Licence

Pro vlastní kód projektu zatím není uvedena samostatná licence. Závislosti se řídí vlastními licencemi.
