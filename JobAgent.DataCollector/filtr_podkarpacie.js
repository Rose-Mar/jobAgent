const fs = require("fs");

const INPUT_FILE = "firmy_z_adresami_final.csv";
const OUTPUT_FILE = "firmy_małopolskie.csv";

function filterData() {
  if (!fs.existsSync(INPUT_FILE)) {
    console.error("Nie znaleziono pliku wejściowego!");
    return;
  }

  const data = fs.readFileSync(INPUT_FILE, "utf8");
  const lines = data.split(/\r?\n/);

  const header = lines[0];
  const filteredLines = lines.filter((line, index) => {
    if (index === 0) return false;

    return line.toUpperCase().includes("MAŁOPOLSKIE");
  });

  const finalContent = [header, ...filteredLines].join("\n");
  fs.writeFileSync(OUTPUT_FILE, finalContent, "utf8");

  console.log(`Znaleziono ${filteredLines.length} firm.`);
  console.log(`Wynik zapisano w: ${OUTPUT_FILE}`);
}

filterData();
