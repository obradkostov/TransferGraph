const $ = id => document.getElementById(id);

async function loadStops() {
  const stops = await (await fetch("/api/stops")).json();
  for (const s of stops) {
    $("from").add(new Option(s.name, s.id));
    $("to").add(new Option(s.name, s.id));
  }
  $("to").selectedIndex = 1;
}

async function findRoute() {
  const mode = $("mode").value;
  const r = await fetch(`/api/route?from=${$("from").value}&to=${$("to").value}&mode=${mode}`);
  const d = await r.json();
  if (!r.ok) { $("out").textContent = d.error; return; }

  // u režimu "transfers" svaki drugi element je linija, ne stanica
  const tok = d.tok.map((x, i) => mode === "transfers" && i % 2 ? `[linija ${x}]` : x).join(" → ");
  const info = [];
  if (d.stanica !== undefined) info.push(`${d.stanica} deonice`);
  if (d.minuta !== undefined) info.push(`${d.minuta} min`);
  if (d.presedanja !== undefined) info.push(`presedanja: ${d.presedanja}`);
  $("out").textContent = `${tok}\n${info.join(" · ")}`;
  $("out").style.whiteSpace = "pre-line";
}

$("go").onclick = findRoute;
loadStops();