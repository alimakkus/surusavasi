  toTitle();
  window.__art2 = (function () {
    function render(w, h, k, fn) {
      var c = document.createElement('canvas'); c.width = w * k; c.height = h * k; var x = c.getContext('2d'); x.scale(k, k);
      var old = ctx, bn = bigNum; ctx = x; bigNum = function () {};
      try { fn(x); } finally { ctx = old; bigNum = bn; }
      return c;
    }
    function sheet(cols, rows, cell, cells) {
      var c = document.createElement('canvas'); c.width = cols * cell; c.height = rows * cell; var x = c.getContext('2d');
      cells.forEach(function (cv, i) { x.drawImage(cv, (i % cols) * cell, Math.floor(i / cols) * cell); });
      return c.toDataURL('image/png');
    }
    var KS = ['barracks', 'armory', 'mine', 'quarry', 'farm', 'hospital', 'tower', 'market'];
    return {
      kinds: KS,
      bld: function (k) {
        var cells = [];
        for (var L = 1; L <= 20; L++) (function (L) { cells.push(render(160, 160, 2, function (x) { x.translate(80, 124); x.scale(1.12, 1.12); bldDraw(k, L, 0); })); })(L);
        return sheet(5, 4, 320, cells);
      },
      misc: function () {
        var cells = [];
        cells.push(render(160, 160, 2, function () { foundation(80, 116, 0, false); }));
        cells.push(render(160, 160, 2, function () { foundation(80, 116, 0, true); }));
        cells.push(render(160, 160, 2, function (x) { // işçi
          x.fillStyle = 'rgba(0,0,0,.3)'; x.beginPath(); x.ellipse(80, 119, 8, 3, 0, 0, TAU); x.fill();
          x.fillStyle = '#8a5a2e'; x.beginPath(); x.ellipse(80, 109, 5, 7, 0, 0, TAU); x.fill();
          x.fillStyle = '#f0c9a0'; x.beginPath(); x.arc(80, 100, 4, 0, TAU); x.fill();
          x.fillStyle = '#ffd23f'; x.beginPath(); x.arc(80, 98, 4.4, Math.PI, 0); x.fill(); x.fillRect(75, 98, 10, 1.6);
        }));
        cells.push(render(160, 160, 2, function (x) { x.fillStyle = '#a3763d'; x.fillRect(76, 112, 8, 6); x.strokeStyle = '#5a3a1e'; x.lineWidth = 1; x.strokeRect(76, 112, 8, 6); })); // sandık (yük)
        cells.push(render(160, 160, 2, function (x) { x.translate(80, 116); x.fillStyle = '#7a5a32'; x.fillRect(-1, -6, 2, 9); x.fillStyle = '#c3ccd4'; x.fillRect(-4, -8, 8, 4); })); // çekiç
        cells.push(render(160, 160, 2, function (x) { // görev panosu
          var bx = 80, by = 110; x.fillStyle = 'rgba(0,0,0,.25)'; x.beginPath(); x.ellipse(bx, by + 8, 26, 6, 0, 0, TAU); x.fill();
          x.fillStyle = '#5a3a1e'; x.fillRect(bx - 22, by - 28, 4, 38); x.fillRect(bx + 18, by - 28, 4, 38);
          x.fillStyle = '#8a5a2e'; rr(x, bx - 28, by - 36, 56, 32, 3); x.fill(); x.fillStyle = '#b98a52'; rr(x, bx - 25, by - 33, 50, 26, 2); x.fill();
          x.fillStyle = '#f4ecd2'; x.fillRect(bx - 20, by - 30, 15, 11); x.fillRect(bx + 2, by - 28, 16, 12); x.fillRect(bx - 12, by - 17, 13, 9);
        }));
        cells.push(render(160, 160, 2, function (x) { // hazine sandığı
          var cx = 80, cy = 110; x.fillStyle = 'rgba(0,0,0,.3)'; x.beginPath(); x.ellipse(cx, cy + 8, 18, 5, 0, 0, TAU); x.fill();
          x.fillStyle = '#8a5a2e'; rr(x, cx - 15, cy - 10, 30, 18, 3); x.fill(); x.fillStyle = '#b6702c'; rr(x, cx - 15, cy - 16, 30, 10, 5); x.fill();
          x.fillStyle = '#ffd23f'; x.fillRect(cx - 15, cy - 8, 30, 3); x.fillRect(cx - 3, cy - 10, 6, 8);
        }));
        return sheet(7, 1, 320, cells);
      },
      ground: function () { return bbgc.toDataURL('image/png'); },
      ring: function (L) { SAVE.b.wall = L; var c = render(W, WH, 2, function () { drawRing(0); }); return c.toDataURL('image/png'); },
      gate: function (L) { SAVE.b.wall = L; var p = BPOS.wall; var c = render(160, 160, 2, function (x) { x.translate(80, 116); drawGate(p[0], p[1], L, 0); }); return c.toDataURL('image/png'); },
      title: function (t) { G.shake = 0; var c = render(W, H, 1.5, function () { drawTitle(t); }); return c.toDataURL('image/jpeg', .88); },
      logo: function () {
        var c = document.createElement('canvas'); c.width = 720; c.height = 400; var x = c.getContext('2d');
        x.textAlign = 'center'; x.textBaseline = 'middle'; x.lineJoin = 'round';
        ['SÜRÜ', 'SAVAŞI'].forEach(function (s, i) {
          var y = 130 + i * 140; x.font = '900 ' + (i ? 128 : 136) + 'px system-ui, "DejaVu Sans", Arial, sans-serif';
          x.shadowColor = 'rgba(255,170,80,.75)'; x.shadowBlur = 34; x.lineWidth = 22; x.strokeStyle = '#2a1650'; x.strokeText(s, 360, y + 6);
          x.shadowBlur = 0; x.lineWidth = 12; x.strokeStyle = '#5b3aa8'; x.strokeText(s, 360, y);
          var g = x.createLinearGradient(0, y - 60, 0, y + 60); g.addColorStop(0, '#fff2b0'); g.addColorStop(.5, '#ffc64a'); g.addColorStop(1, '#ff8a2a');
          x.fillStyle = g; x.fillText(s, 360, y);
        });
        return c.toDataURL('image/png');
      }
    };
  })();