const fs = require("fs");

const inputFile = "firmy_pkd_data.csv";
const outputFile = "wyselekcjonowane_it.csv";
const targetPKD = "62.01.Z";

try {
  const data = fs.readFileSync(inputFile, "utf8");
  const lines = data.split(/\r?\n/);
  const header = lines[0];

  const filteredLines = lines.filter((line, index) => {
    if (index === 0 || !line.trim()) return false;

    const columns = line.split('","');

    const pkdGlowny = columns[3] ? columns[3].replace(/"/g, "").trim() : "";

    return pkdGlowny === targetPKD;
  });

  fs.writeFileSync(outputFile, [header, ...filteredLines].join("\n"), "utf8");
  console.log(` Znaleziono ${filteredLines.length} firm.`);
  console.log(`Nowy plik: ${outputFile}`);
} catch (err) {
  console.error("Błąd:", err.message);
}
