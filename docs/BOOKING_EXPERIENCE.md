# Objednávání a klientská správa termínu

## Funkce pro návštěvníka

- „Najít nejbližší termín“ vyhledá první dostupný čas od dneška v horizontu 60 dní pro délku vybrané služby. Respektuje skutečnou otevírací dobu, výjimky a obsazenost. Vybere den i čas; klient pak doplní údaje. Dostupnost se znovu ověřuje při uložení.
- Na menších obrazovkách jsou dole tlačítka „Zavolat“ a „Objednat“. Při zobrazení rezervačního formuláře se skryjí. Cookie panel má vlastní prostor nad nimi.
- Po objednání přijde e-mail se dvěma nezávislými odkazy. Potvrzovací odkaz platí 15 minut. Odkaz pro správu umožňuje po potvrzení rezervace změnit čas nebo rezervaci zrušit, a platí do začátku návštěvy.
- Přesun zachovává službu a délku návštěvy. Při konfliktu zůstane původní rezervace platná. Při stornu klient nejprve výslovně potvrdí zrušení.
- Potvrzenou návštěvu lze stáhnout do kalendáře jako `.ics` s připomínkou hodinu před začátkem. Časy jsou v UTC, kalendář je zobrazí podle vlastního časového pásma. Soubor obsahuje název ordinace, adresu, jméno mazlíčka a důvod návštěvy, nikoli soukromý přístupový odkaz.
- Stránka vysvětluje postup v krátké sekci otázek k objednávání. Neuvádí neověřené ceny, vybavení, zkušenosti lékaře ani smyšlené reference.

## Meze a pravidla

Připomínka je součástí importované kalendářové události, nikoli samostatný e-mail nebo SMS. Její skutečné zobrazení závisí na kalendářové aplikaci a nastavení oznámení. Přesun ani storno automaticky nesynchronizují již importovanou událost; klient ji musí upravit nebo odstranit. Stránka na to při přesunu upozorňuje.

Ztracený odkaz se neobnovuje jen podle čísla rezervace nebo zadaného e-mailu. Klient kontaktuje ordinaci. Starší rezervace a ruční zápisy žádný nový soukromý odkaz automaticky nezískávají; nadále je spravuje ordinace. Nový e-mail se posílá pouze při novém veřejném objednání.

## Ochrana a nasazení

Potvrzovací i správcovský token mají po 32 kryptograficky náhodných bajtech. Databáze ukládá pouze jejich SHA-256 otisky. Správcovský token nepřichází v odpovědi při vytváření rezervace a nefunguje jako potvrzovací token. Do API se předává v těle POST požadavku s CSRF ochranou a limitem požadavků. Stránka přečte fragment odkazu a odstraní jej z adresního řádku. Opětovné otevření odkazu ve stejné kartě načte stav znovu. API odpovědi se neukládají do cache a diagnostika nezaznamenává tokeny ani kontaktní údaje.

Přesun i storno ověřují platnost odkazu uvnitř databázové transakce. Přesun kontroluje obsazenost pod stejným SQLite zámkem jako vytváření rezervací. Klientská správa vrací pouze údaje o dané návštěvě potřebné pro tuto obrazovku, nikoli kontakt či jméno majitele. Při stornu se odkaz zneplatní.

Před nasazením nové verze je nutné běžným řízeným postupem aplikovat migraci `20261019210828_BookingSelfService`, která přidává nullable sloupec a unikátní index. Stávající rezervace se nemažou. Postup záloh a inicializace je v [PRODUCTION.md](PRODUCTION.md). Původní schéma obnovujte pouze z ověřené zálohy společně s odpovídající verzí aplikace.

V lokálním demu jsou oba odkazy v testovací schránce `/api/dev/mailbox/view`. Po restartu API se obsah schránky vymaže; uložené rezervace zůstávají.

## Ověření

`npm run qa` ověřuje i souběžné přesuny, odmítnutí neplatných odkazů a požadavků bez CSRF, zachování původního času při konfliktu, hledání přes zavřený víkend, export kalendáře a kompletní klientský tok v Chromu. Ručně byl v lokálním webu vyzkoušen výběr nejbližšího času, objednání, potvrzení, přesun a storno.
