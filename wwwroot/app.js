const $ = id => document.getElementById(id);
let stops = [];

// ---------- Stanice u padajucim listama ----------
async function loadStops() {
  stops = await (await fetch("/api/stops")).json();
  for (const id of ["from", "to", "rFrom", "cStop", "aTo", "dStop"]) {
    const sel = $(id), keep = sel.value;
    sel.innerHTML = "";
    for (const s of stops) sel.add(new Option(s.name + (s.closed ? " (zatvorena)" : ""), s.id));
    if (keep) sel.value = keep;
  }
  if (!$("to").dataset.init) { $("to").selectedIndex = 1; $("to").dataset.init = "1"; }
}

// ---------- Put od A do B ----------
async function findRoute() {
  const mode = $("mode").value;
  const r = await fetch(`/api/route?from=${$("from").value}&to=${$("to").value}&mode=${mode}`);
  const d = await r.json();
  if (!r.ok) { $("out").textContent = d.error; return; }

  // u rezimu "transfers" svaki drugi element je linija, a ne stanica
  const tok = d.tok.map((x, i) => mode === "transfers" && i % 2 ? `[linija ${x}]` : x).join(" → ");
  const info = [];
  if (d.stanica !== undefined) info.push(`Broj deonica: ${d.stanica}`);
  if (d.minuta !== undefined) info.push(`Vreme: ${d.minuta} min`);
  if (d.presedanja !== undefined) info.push(`Presedanja: ${d.presedanja}`);
  $("out").textContent = `${tok}\n${info.join(" · ")}`;
}

// ---------- Domet ----------
async function reach() {
  const r = await fetch(`/api/reach?from=${$("rFrom").value}&minutes=${$("rMin").value}`);
  const d = await r.json();
  $("rOut").textContent = d.length
    ? d.map(x => `${x.stanica} (${x.minuta} min)`).join("\n")
    : "Nema stanica u dometu.";
}

// ---------- Cvorista ----------
async function loadHubs() {
  const d = await (await fetch("/api/hubs")).json();
  $("hubs").textContent = d.map(x => `${x.stanica}: linije ${x.linije.join(", ")}`).join("\n");
}

// ---------- Zatvaranje stanice ----------
async function toggleClosed() {
  const s = stops.find(x => x.id === $("cStop").value);
  const r = await fetch(`/api/stops/${s.id}/closed?closed=${!s.closed}`, { method: "POST" });
  if (!r.ok) { $("cOut").textContent = "Greška na serveru."; return; }
  $("cOut").textContent = `Stanica ${s.name}: ${s.closed ? "otvorena" : "zatvorena"}`;
  await loadStops();
}

// ---------- Nova stanica ----------
async function addStop() {
  const name = $("aName").value;
  const r = await fetch("/api/stops", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name, connectTo: $("aTo").value, line: $("aLine").value, minutes: Number($("aMin").value) })
  });
  const d = await r.json();
  $("aOut").textContent = r.ok ? `Stanica ${name} je dodata.` : d.error;
  if (r.ok) { $("aName").value = ""; await loadStops(); await loadHubs(); }
}
async function deleteStop() {
  const s = stops.find(x => x.id === $("dStop").value);
  if (!confirm(`Obrisati stanicu ${s.name}?`)) return;
  const r = await fetch(`/api/stops/${s.id}`, { method: "DELETE" });
  const d = await r.json().catch(() => ({}));
  $("dOut").textContent = r.ok ? `Stanica ${s.name} je obrisana.` : d.error;
  if (r.ok) { await loadStops(); await loadHubs(); }
}
$("dBtn").onclick = deleteStop;
$("go").onclick = findRoute;
$("rGo").onclick = reach;
$("cBtn").onclick = toggleClosed;
$("aBtn").onclick = addStop;

loadStops();
loadHubs();
