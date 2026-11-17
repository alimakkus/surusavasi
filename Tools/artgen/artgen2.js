const { chromium } = require('playwright'); const fs = require('fs');
const S = process.argv[2], OUT = process.argv[3];
(async () => {
  const b = await chromium.launch(); const ctx = await b.newContext({ deviceScaleFactor: 2, viewport: { width: 400, height: 800 } }); const p = await ctx.newPage();
  const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.goto('file://' + S + '/art2.html'); await p.waitForTimeout(800);
  function save(name, url) { fs.writeFileSync(OUT + '/' + name, Buffer.from(url.split(',')[1], 'base64')); }
  const kinds = await p.evaluate(() => __art2.kinds);
  for (const k of kinds) save('bld_' + k + '.png', await p.evaluate(k => __art2.bld(k), k));
  save('base_misc.png', await p.evaluate(() => __art2.misc()));
  save('base_ground.png', await p.evaluate(() => __art2.ground()));
  const Ls = [0, 1, 5, 9, 13, 17];
  for (let i = 0; i < 6; i++) { save('wall_ring_t' + i + '.png', await p.evaluate(L => __art2.ring(L), Ls[i])); save('wall_gate_t' + i + '.png', await p.evaluate(L => __art2.gate(L), Ls[i])); }
  for (let i = 0; i < 16; i++) save('title_' + String(i).padStart(2, '0') + '.jpg', await p.evaluate(t => __art2.title(t), 5 + i / 8));
  save('logo.png', await p.evaluate(() => __art2.logo()));
  console.log('errors', JSON.stringify(errs)); await b.close();
})();
