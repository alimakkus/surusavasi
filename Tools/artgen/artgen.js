const { chromium } = require('playwright'); const fs = require('fs');
const S = process.argv[2], OUT = process.argv[3];
(async () => {
  const b = await chromium.launch(); const p = await b.newPage();
  const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.goto('file://' + S + '/art.html'); await p.waitForTimeout(800);
  function save(name, url) { fs.writeFileSync(OUT + '/' + name + '.png', Buffer.from(url.split(',')[1], 'base64')); }
  for (let m = 0; m < 20; m++) { save('enemies_m' + String(m).padStart(2, '0'), await p.evaluate(m => __art.enemies(m), m)); save('bg_m' + String(m).padStart(2, '0'), await p.evaluate(m => __art.bg(m), m)); }
  save('bosses', await p.evaluate(() => __art.bosses()));
  save('elites', await p.evaluate(() => __art.elites()));
  save('units', await p.evaluate(() => __art.units()));
  save('props', await p.evaluate(() => __art.props()));
  save('trees', await p.evaluate(() => __art.trees()));
  console.log('errors', JSON.stringify(errs)); await b.close();
})();
