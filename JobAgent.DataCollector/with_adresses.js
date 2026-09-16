const https = require("https");
const fs = require("fs");

const API_KEY = process.env.GUS_API_KEY;
const INPUT_FILE = "wyselekcjonowane_it.csv";
const OUTPUT_FILE = "firmy_z_adresami_final.csv";

const ENDPOINT =
  "https://wyszukiwarkaregon.stat.gov.pl/wsBIR/UslugaBIRzewnPubl.svc";

let sid = null;

function postSoap(soapEnvelope, sessionId = null) {
  return new Promise((resolve, reject) => {
    const bodyBuffer = Buffer.from(soapEnvelope, "utf-8");
    const options = {
      hostname: "wyszukiwarkaregon.stat.gov.pl",
      path: "/wsBIR/UslugaBIRzewnPubl.svc",
      method: "POST",
      headers: {
        "Content-Type": "application/soap+xml; charset=utf-8",
        "Content-Length": bodyBuffer.length,
        Accept: "application/soap+xml, text/xml",
      },
    };

    if (sessionId) options.headers["sid"] = sessionId;

    const req = https.request(options, (res) => {
      let data = "";
      res.on("data", (chunk) => (data += chunk));
      res.on("end", () => {
        if (res.statusCode !== 200) {
          reject(
            new Error(`HTTP ${res.statusCode}: ${data.substring(0, 800)}`),
          );
        } else {
          resolve(data);
        }
      });
    });

    req.on("error", (e) => reject(e));
    req.write(bodyBuffer);
    req.end();
  });
}

async function login() {
  const envelope = `<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"
               xmlns:a="http://www.w3.org/2005/08/addressing"
               xmlns:ns="http://CIS/BIR/PUBL/2014/07">
  <soap:Header>
    <a:Action>http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/Zaloguj</a:Action>
    <a:To>${ENDPOINT}</a:To>
  </soap:Header>
  <soap:Body>
    <ns:Zaloguj>
      <ns:pKluczUzytkownika>${API_KEY}</ns:pKluczUzytkownika>
    </ns:Zaloguj>
  </soap:Body>
</soap:Envelope>`;

  try {
    const response = await postSoap(envelope);
    const match = response.match(/<ZalogujResult>(.*?)<\/ZalogujResult>/);
    sid = match ? match[1] : null;
    if (!sid)
      console.error("Brak SID w odpowiedzi:", response.substring(0, 400));
    return !!sid;
  } catch (err) {
    console.error("Błąd logowania:", err.message);
    return false;
  }
}

async function getCompanyData(nip) {
  const envelope = `<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope"
               xmlns:a="http://www.w3.org/2005/08/addressing"
               xmlns:ns="http://CIS/BIR/PUBL/2014/07"
               xmlns:dat="http://CIS/BIR/PUBL/2014/07/DataContract">
  <soap:Header>
    <a:Action>http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/DaneSzukajPodmioty</a:Action>
    <a:To>${ENDPOINT}</a:To>
  </soap:Header>
  <soap:Body>
    <ns:DaneSzukajPodmioty>
      <ns:pParametryWyszukiwania>
        <dat:Nip>${nip}</dat:Nip>
      </ns:pParametryWyszukiwania>
    </ns:DaneSzukajPodmioty>
  </soap:Body>
</soap:Envelope>`;

  try {
    const response = await postSoap(envelope, sid);

    const resultXmlMatch = response.match(
      /<DaneSzukajPodmiotyResult>([\s\S]*?)<\/DaneSzukajPodmiotyResult>/,
    );
    if (!resultXmlMatch || !resultXmlMatch[1].trim()) return null;

    const res = resultXmlMatch[1]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&");

    return {
      nazwa: res.match(/<Nazwa>(.*?)<\/Nazwa>/)?.[1] || "",
      woj: res.match(/<Wojewodztwo>(.*?)<\/Wojewodztwo>/)?.[1] || "",
      miasto: res.match(/<Miejscowosc>(.*?)<\/Miejscowosc>/)?.[1] || "",
      kod: res.match(/<KodPocztowy>(.*?)<\/KodPocztowy>/)?.[1] || "",
      ulica: res.match(/<Ulica>(.*?)<\/Ulica>/)?.[1] || "",
      nr: res.match(/<NrNieruchomosci>(.*?)<\/NrNieruchomosci>/)?.[1] || "",
    };
  } catch (err) {
    console.error(`Błąd dla NIP ${nip}:`, err.message);
    if (err.message.includes("401") || err.message.includes("403")) {
      console.log("Próba ponownego logowania...");
      await login();
    }
    return null;
  }
}

async function start() {
  if (!(await login())) {
    console.error("Nie udało się zalogować. Sprawdź klucz API.");
    return;
  }

  const raw = fs.readFileSync(INPUT_FILE, "utf8");
  const lines = raw.split(/\r?\n/).filter((l) => l.trim());
  let results = [lines[0] + ',"Wojewodztwo","Kod","Miasto","Ulica"'];

  for (let i = 1; i < lines.length; i++) {
    const columns = lines[i].split('","');
    const nip = columns[2]?.replace(/"/g, "").trim();

    if (nip && nip.length === 10) {
      const info = await getCompanyData(nip);
      if (info) {
        results.push(
          `${lines[i]},"${info.woj}","${info.kod}","${info.miasto}","${info.ulica} ${info.nr}"`,
        );
        console.log(`[${i}/${lines.length - 1}] ${nip} -> ${info.miasto}`);
      } else {
        results.push(`${lines[i]},"","","",""`);
        console.log(`[${i}/${lines.length - 1}]  ${nip} -> brak danych`);
      }
    } else {
      results.push(`${lines[i]},"","","",""`);
    }

    if (i % 20 === 0) fs.writeFileSync(OUTPUT_FILE, results.join("\n"), "utf8");
    await new Promise((r) => setTimeout(r, 250));
  }

  fs.writeFileSync(OUTPUT_FILE, results.join("\n"), "utf8");
  console.log("wynik zapisany w:", OUTPUT_FILE);
}

start();
