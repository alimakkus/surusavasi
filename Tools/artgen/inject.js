  window.__art = (function () {
    function render(w, h, k, fn) {
      var c = document.createElement('canvas'); c.width = w * k; c.height = h * k; var x = c.getContext('2d'); x.scale(k, k);
      var old = ctx, bn = bigNum; ctx = x; bigNum = function () {};
      try { fn(); } finally { ctx = old; bigNum = bn; }
      return c;
    }
    function sheet(cols, rows, cell, cells) {
      var c = document.createElement('canvas'); c.width = cols * cell; c.height = rows * cell; var x = c.getContext('2d');
      cells.forEach(function (cv, i) { x.drawImage(cv, (i % cols) * cell, Math.floor(i / cols) * cell); });
      return c.toDataURL('image/png');
    }
    var ALL = ['walker', 'runner', 'spitter', 'armored', 'splitter', 'brute', 'wraith', 'giant', 'mummy', 'frost', 'ember', 'toxic'];
    var PH = [0, Math.PI * .5];
    return {
      enemies: function (m) {
        applyMap(m); G = G || {}; var oldMap = G.map; G.map = m; var cells = [];
        ALL.forEach(function (tk) {
          var T = TYPES[tk];
          PH.forEach(function (ph) {
            cells.push(render(128, 128, 2, function () {
              if (T.big) bigZombie(64, 100, T.sc, ph * 2, T.cfg);
              else { var offs = CL[T.cl].slice(0).sort(function (p, q) { return p[1] - q[1]; });
                for (var i = 0; i < offs.length; i++) zombie(64 + offs[i][0] * 1.3, 100 + offs[i][1] * 1.3, T.sc * 1.4, ph * 3 + i * 1.7, 3 + i, mst(T)); }
            }));
          });
        });
        G.map = oldMap;
        return sheet(8, 3, 256, cells);
      },
      bosses: function () {
        var cells = [];
        BOSSES.forEach(function (bc, i) { PH.forEach(function (ph) { cells.push(render(200, 200, 2, function () { bigZombie(100, 170, 3.9, ph * 2, bc); })); }); });
        return sheet(5, 4, 400, cells);
      },
      elites: function () {
        applyMap(0); var cells = [];
        TORDER.forEach(function (tk) {
          var et = TYPES[tk];
          var cfg = et.big ? et.cfg : { skin: (et.st.skin || '#8dbd80'), shirt: (et.st.shirt || '#4b3b52'), eye: (et.st.eye || '#e9f26c'), hat: et.st.helm ? 'helm' : null, aura: null };
          PH.forEach(function (ph) { cells.push(render(128, 128, 2, function () { bigZombie(64, 110, 2.6, ph * 2, cfg); })); });
        });
        return sheet(8, 2, 256, cells);
      },
      units: function () {
        var cells = [];
        ['ayaz', 'kaya', 'mira'].forEach(function (id) { HSEL = id; PH.forEach(function (ph) { cells.push(render(96, 96, 2, function () { hero(48, 62, ph); })); }); });
        HSEL = 'ayaz';
        [0, 1, 2, 3].forEach(function (k) { cells.push(render(96, 96, 2, function () { soldier(48, 62, 1.4, k * Math.PI / 2, false); })); });
        return sheet(10, 1, 192, cells);
      },
      props: function () {
        var cells = [];
        cells.push(render(128, 128, 2, function () { barrel(64, 64, 0, 0); }));
        ['double', 'rapid', 'blast'].forEach(function (pk) { cells.push(render(128, 128, 2, function () { powerBox(64, 64, pk, 0); })); });
        cells.push(render(128, 128, 2, function () { board(64, 64, 0, 0); }));
        return sheet(5, 1, 256, cells);
      },
      trees: function () {
        var cells = [];
        MAPS.forEach(function (M) { cells.push(render(128, 128, 2, function () { TREEST = M.tree; tree(64, 100, 1); TREEST = null; })); });
        return sheet(8, 3, 256, cells);
      },
      bg: function (m) { var c = document.createElement('canvas'); c.width = W; c.height = TILE; buildBgOn(c.getContext('2d'), MAPS[m], 11 + m * 31); return c.toDataURL('image/png'); }
    };
  })();
