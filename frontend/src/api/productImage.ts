/**
 * The catalogue is seeded with images from an external placeholder service, which costs roughly two
 * seconds per card and turns a 2 ms API response into a slow-feeling page. These stand-ins are
 * generated locally, so a card paints immediately and the app works offline.
 */

// Deliberately muted so the artwork stays background, not decoration competing with the product name.
const PALETTES: ReadonlyArray<readonly [string, string]> = [
  ['#e8f1ef', '#c9e0da'],
  ['#eef0f7', '#d5daea'],
  ['#f6eee8', '#e6d3c4'],
  ['#eaf1e8', '#cfe0ca'],
  ['#f3ecf3', '#ddcadd'],
  ['#e9eff5', '#cadbe8'],
];

function hash(value: string): number {
  let result = 0;
  for (let i = 0; i < value.length; i += 1) {
    result = (result * 31 + value.charCodeAt(i)) | 0;
  }
  return Math.abs(result);
}

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]!.toUpperCase())
    .join('');
}

function placeholder(name: string): string {
  const seed = hash(name);
  const [from, to] = PALETTES[seed % PALETTES.length]!;
  const rotation = seed % 90;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600" role="img">
<defs><linearGradient id="g" gradientTransform="rotate(${rotation})">
<stop offset="0%" stop-color="${from}"/><stop offset="100%" stop-color="${to}"/>
</linearGradient></defs>
<rect width="600" height="600" fill="url(#g)"/>
<circle cx="${140 + (seed % 320)}" cy="${180 + (seed % 240)}" r="${90 + (seed % 70)}" fill="#ffffff" opacity="0.35"/>
<text x="300" y="300" text-anchor="middle" dominant-baseline="central"
 font-family="Inter, system-ui, sans-serif" font-size="150" font-weight="600"
 fill="#1b2430" opacity="0.28">${initials(name)}</text>
</svg>`;

  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
}

/** Uses a shipped asset when there is one; otherwise draws a stand-in rather than fetching one. */
export function productImage(name: string, imageUrl?: string | null): string {
  return imageUrl?.startsWith('/') ? imageUrl : placeholder(name);
}
