const $ = id => document.getElementById(id);
let stops = [];

async function loadStops() {
  stops = await (await fetch("/api/stops")).json();
  for (const id of ["from", "to", "rFrom", "cStop"]) {
    const sel = $(id), keep = sel.value;
    sel.innerHTML = "";
    for (const s of stops) sel.add(new Option(s.name + (s.closed ? " (zatvorena)" : ""), s.id));
    if (keep) sel.value = keep;
  }
  if (!$("to").dataset.init) { $("to").selectedIndex = 1; $("to").dataset.init = "1"; }
}

async function findRoute() {
  const mode = $("mode").value;
  const r = await fetch(`/api/route?from=${$("from").value}&to=${$("to").value}&mode=${mode}`);
  const d = await r.json();
  if (!r.ok) { $("out").textContent = d.error; return; }

  const tok = d.tok.map((x, i) => mode === "transfers" && i % 2 ? `[linija ${x}]` : x).join(" → ");
  const info = [];
  if (d.stanica !== undefined) info.push(`${d.stanica} deonice`);
  if (d.minuta !== undefined) info.push(`${d.minuta} min`);
  if (d.presedanja !== undefined) info.push(`presedanja: ${d.presedanja}`);
  $("out").textContent = `${tok}\n${info.join(" · ")}`;
}

async function reach() {
  const r = await fetch(`/api/reach?from=${$("rFrom").value}&minutes=${$("rMin").value}`);
  const d = await r.json();
  $("rOut").textContent = d.length
    ? d.map(x => `${x.stanica} (${x.minuta} min)`).join("\n")
    : "Nema stanica u dometu.";
}

async function loadHubs() {
  const d = await (await fetch("/api/hubs")).json();
  $("hubs").textContent = d.map(x => `${x.stanica}: linije ${x.linije.join(", ")}`).join("\n");
}

async function toggleClosed() {
  const s = stops.find(x => x.id === $("cStop").value);
  const r = await fetch(`/api/stops/${s.id}/closed?closed=${!s.closed}`, { method: "POST" });
  if (!r.ok) { $("cOut").textContent = "Greška na serveru."; return; }
  $("cOut").textContent = `${s.name}: ${s.closed ? "ponovo otvorena" : "zatvorena"}`;
  await loadStops();
}

$("go").onclick = findRoute;
$("rGo").onclick = reach;
$("cBtn").onclick = toggleClosed;
loadStops();
loadHubs();