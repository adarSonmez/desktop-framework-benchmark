// Deterministic 10k-item dataset generator. Usage: node spec/gen-data.mjs
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const COUNT = 10_000;

// mulberry32 PRNG, fixed seed
let s = 0x5eed1234;
const rnd = () => {
  s |= 0; s = (s + 0x6d2b79f5) | 0;
  let t = Math.imul(s ^ (s >>> 15), 1 | s);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};
const pick = (a) => a[Math.floor(rnd() * a.length)];

const adjectives = ["Amber", "Brisk", "Cobalt", "Dusty", "Ember", "Frost", "Golden", "Hollow", "Ivory", "Jade", "Keen", "Lunar", "Misty", "Noble", "Onyx", "Polar", "Quiet", "Rapid", "Silver", "Tidal", "Umber", "Vivid", "Wild", "Zephyr"];
const nouns = ["Anchor", "Beacon", "Canvas", "Drifter", "Engine", "Falcon", "Garden", "Harbor", "Island", "Junction", "Kettle", "Lantern", "Meadow", "Nomad", "Orbit", "Pioneer", "Quarry", "Ridge", "Summit", "Thicket", "Utopia", "Voyager", "Willow", "Yonder"];
const categories = ["Electronics", "Books", "Garden", "Kitchen", "Sports", "Toys", "Office", "Health"];

const base = Date.UTC(2020, 0, 1);
const items = [];
for (let i = 1; i <= COUNT; i++) {
  items.push({
    id: i,
    name: `${pick(adjectives)} ${pick(nouns)} ${String(i).padStart(5, "0")}`,
    category: pick(categories),
    price: Math.round(rnd() * 99900 + 100) / 100,
    stock: Math.floor(rnd() * 1000),
    createdAt: new Date(base + Math.floor(rnd() * 5 * 365 * 86400000)).toISOString().slice(0, 10),
  });
}

const out = join(here, "data", "items.json");
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify(items));
console.log(`wrote ${items.length} items -> ${out}`);
