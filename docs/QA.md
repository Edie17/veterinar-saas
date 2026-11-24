# Lokální testování a automatické QA

## Ruční vyzkoušení

Potřebujete .NET SDK 10, Node.js alespoň 22.12 a pro automatické prohlížečové testy Google Chrome. První běh Selenium může stáhnout odpovídající ovladač a potřebuje přístup k internetu.

Z kořene spusťte `npm run dev`. Stejný příkaz funguje i z adresáře `client`. Spustí API i frontend; již běžící demo API nebo frontend znovu použije. Ukončení přes Ctrl+C zastaví pouze procesy vytvořené tímto příkazem.

- Web: http://localhost:5173
- Administrace: http://localhost:5173/sprava
- Demo přihlášení: `admin@example.cz` / `ChangeMe123!`
- Testovací e-maily: http://localhost:5173/api/dev/mailbox/view

Demo používá samostatnou databázi `.local/veterina-demo.db`. Obsahuje ordinační hodiny, služby, publikovaný článek a dva potvrzené syntetické pacienty na následující pracovní den. V rezervaci vyberte budoucí pracovní den, službu a volný čas, vyplňte údaje a odešlete formulář. Ve schránce otevřete potvrzovací odkaz a potvrďte rezervaci. V administraci pak uvidíte pacienta v kalendáři a můžete rezervaci stornovat.

Testovací schránka nic neposílá přes SMTP. Po restartu API je prázdná; již uložené rezervace zůstávají. Pro novou rezervaci proto použijte nový termín. Ukázková data se při opakovaném startu neduplikují. Pro úplně čisté demo zastavte vlastní demo proces a odstraňte pouze `.local/veterina-demo.db`.

Demo je blokované mimo Development a jeho schránka přijímá pouze požadavky z loopbacku. Nikdy jej nenasazujte jako veřejně dostupné prostředí. Produkční nasazení používá vlastní konfiguraci podle [PRODUCTION.md](PRODUCTION.md).

Pokud API chybí nebo vrátí chybu při načítání služeb, rezervační formulář nyní zobrazí vysvětlení a tlačítko pro opakované načtení. Nezůstane pouze beze slova zakázaný.

## Jeden příkaz pro QA

```sh
npm ci --prefix client
npm run qa
```

QA obnoví uzamčené backendové závislosti, spustí backendové testy, Vitest, kontrolu TypeScriptu a produkční build. Publikuje API s hotovým frontendem, spustí vlastní server na volném loopback portu s novou SQLite databází a provede Selenium scénáře ve skutečném headless Chromu. Server po běhu ukončí. Nepoužívá vaši demo databázi ani přihlášení v běžném prohlížeči.

| Vrstva | Aktuální rozsah |
| --- | --- |
| Backend: 65 testů | Doménová a aplikační pravidla, databáze, HTTP, autentizace a CSRF, rezervace a jejich souběh, bezpečnostní konfigurace, izolace dema a schránka; nejbližší termín, soukromé odkazy, přesun, storno, souběžné přesuny a zachování původního termínu při konfliktu |
| Frontend: 24 testů | Validace údajů, Praha a změna času, sanitizace článků, API chyby a CSRF, obnova po výpadku služeb, interakce s rezervací a přihlášením; nejbližší termín, export kalendáře, správa rezervace, opětovné otevření soukromého odkazu; menu a klávesnice, rozlišení načítání článku od chyby a chybějícího článku |
| Prohlížeč: 10 scénářů | Domovská stránka, navigace, články a mobilní tlačítka; odmítnutí skriptů; vytvoření rezervace; potvrzení přes schránku, přihlášení, kalendář a storno; změna obsahu, služeb a hodin; editor a publikace článku; ruční objednání; celý klientský tok na mobilní šířce 390 px; samostatný průchod domovskou stránkou, menu, formulářem a článkem v šířkách 320, 390 a 768 px |

Výsledky najdete v `.qa/<čas-běhu>/`: backendové a prohlížečové reporty TRX, `frontend.xml` ve formátu JUnit a `result.json`. Rozšířené prohlížečové scénáře ukládají PNG a HTML; při selhání mají příponu `-FAILED`. `.qa/latest.json` ukazuje na poslední běh. Testovací databáze a publikovaná aplikace jsou ve stejném izolovaném adresáři. `.qa` a `.local` jsou vyloučené z Gitu i kontextu Docker sestavení.

CI spouští `npm run qa` při pull requestu a pushi do `main`. Ukládá reporty, HTML a screenshoty na sedm dní; databázi a publikované binární soubory nenahrává. Následuje sestavení produkčního Docker image.

## Rychlejší jednotlivé kontroly

```sh
npm test --prefix client
npm run test:watch --prefix client
dotnet test api/Veterinar.sln -c Release
```

Samotný backendový příkaz prohlížečové scénáře záměrně přeskakuje. Pro jejich provedení používejte `npm run qa`, který připraví celé prostředí. Selhání libovolného kroku ukončí QA nenulovým návratovým kódem.

## Meze ověření

Scénáře pokrývají hlavní uživatelské cesty, nikoli každou kombinaci vstupů nebo všechny prohlížeče. Mobilní kontrola používá emulaci viewportu Chromu při šířkách 320, 390 a 768 px. Syntetický dlouhý nadpis, souvislý text a široká tabulka se pro kontrolu rozložení přidají pouze do DOM testovací karty; databáze se tím nemění. Kontroluje se šířka stránky, rozměry ovládacích prvků a font formuláře. Screenshoty zachycují také menu a formulář. Tento běh neověřuje skutečné doručení SMTP, produkční HTTPS a reverzní proxy, obnovu záloh, zátěž, skutečnou klávesnici telefonu ani úplnou přístupnost. Tyto kontroly je potřeba provést v cílovém nasazení; úspěšné QA samo o sobě není potvrzením produkční připravenosti.
