'use strict';
(function () {
    const $ = id => document.getElementById(id);
    const STANDARD = 230, TOLERANCE = 23;
    const UPPER = STANDARD + TOLERANCE, LOWER = STANDARD - TOLERANCE;

    let canvas, ctx, data = [], compareData = [], stats = {};
    let panX = 0, scaleX = 1, dragging = false, dragStartX = 0, dragStartPan = 0;
    let animFrame = null, connection = null, liveInterval = null;
    let hoverIdx = -1, hoverX = 0, hoverY = 0;
    let plotState = null; // cached transform from last draw

    function init() {
        canvas = $('chart');
        ctx = canvas.getContext('2d');
        resize();
        drawEmptyChart();
        window.addEventListener('resize', resize);
        canvas.addEventListener('mousedown', onMouseDown);
        canvas.addEventListener('mousemove', onMouseMove);
        canvas.addEventListener('mouseup', onMouseUp);
        canvas.addEventListener('mouseleave', onMouseLeave);
        canvas.addEventListener('wheel', onWheel, { passive: false });
        canvas.addEventListener('touchstart', onTouchStart, { passive: false });
        canvas.addEventListener('touchmove', onTouchMove, { passive: false });
        canvas.addEventListener('touchend', onMouseUp);
        canvas.addEventListener('dblclick', resetZoom);

        $('mode').addEventListener('change', onModeChange);
        $('days').addEventListener('change', fetchData);
        $('smooth').addEventListener('change', fetchData);

        // Defer data loading so UI paints first
        requestAnimationFrame(function () {
            onModeChange();
            initSignalR();
        });
    }

    function drawEmptyChart() {
        var w = canvas.width, h = canvas.height;
        var dpr = devicePixelRatio;
        var pad = { top: 20 * dpr, right: 50 * dpr, bottom: 30 * dpr, left: 55 * dpr };
        var plotW = w - pad.left - pad.right;
        var plotH = h - pad.top - pad.bottom;
        ctx.fillStyle = '#111';
        ctx.fillRect(0, 0, w, h);
        // Grid placeholder
        ctx.strokeStyle = '#222';
        ctx.lineWidth = dpr;
        for (var i = 0; i <= 5; i++) {
            var y = pad.top + (plotH / 5) * i;
            ctx.beginPath(); ctx.moveTo(pad.left, y); ctx.lineTo(w - pad.right, y); ctx.stroke();
        }
        for (var j = 0; j <= 6; j++) {
            var x = pad.left + (plotW / 6) * j;
            ctx.beginPath(); ctx.moveTo(x, pad.top); ctx.lineTo(x, h - pad.bottom); ctx.stroke();
        }
        // Border
        ctx.strokeStyle = '#333'; ctx.lineWidth = dpr;
        ctx.strokeRect(pad.left, pad.top, plotW, plotH);
        // Loading text
        ctx.fillStyle = '#444';
        ctx.font = (14 * dpr) + 'px system-ui';
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText('Loading data...', w / 2, h / 2);
    }

    function resize() {
        const r = canvas.parentElement.getBoundingClientRect();
        canvas.width = r.width * devicePixelRatio;
        canvas.height = r.height * devicePixelRatio;
        draw();
    }

    async function fetchData() {
        showLoader(true);
        const mode = $('mode').value;
        const smooth = parseInt($('smooth').value) || 0;
        const body = {
            isLive: mode === 'live',
            today: mode === 'today' || mode === 'compare',
            compare: mode === 'compare',
            days: mode === 'days' ? (parseInt($('days').value) || 7) : null,
            smooth: smooth > 0 ? smooth : null
        };

        try {
            const res = await fetch('/api/voltage/data', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const json = await res.json();
            setError(json.hasError ? json.errorMessage : '');
            data = mapEntries(json.currentEntries);
            compareData = mode === 'compare' ? mapCompareEntries(json.yesterdayEntries, data) : [];
            stats = json.statistics || {};
            updateStats();
            resetZoom();
            $('status').textContent = json.hasError ? 'Configuration error' : data.length + ' pts | ' + new Date().toLocaleTimeString();
        } catch (err) {
            setError(err.message);
            $('status').textContent = 'Error: ' + err.message;
        } finally {
            showLoader(false);
        }
    }

    function mapEntries(entries) {
        return (entries || [])
            .map(function (e) { return [new Date(e.timestamp).getTime(), e.voltage]; })
            .filter(function (p) { return Number.isFinite(p[0]) && Number.isFinite(p[1]); });
    }

    function mapCompareEntries(entries, referenceData) {
        var referenceDate = referenceData.length > 0 ? new Date(referenceData[0][0]) : new Date();
        return (entries || [])
            .map(function (e) {
                var source = new Date(e.timestamp);
                var aligned = new Date(referenceDate);
                aligned.setHours(source.getHours(), source.getMinutes(), source.getSeconds(), source.getMilliseconds());
                return [aligned.getTime(), e.voltage];
            })
            .filter(function (p) { return Number.isFinite(p[0]) && Number.isFinite(p[1]); });
    }

    function initSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/voltage')
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        connection.on('NewReading', function (reading) {
            if ($('mode').value !== 'live') return;
            var t = new Date(reading.timestamp).getTime();
            var v = reading.voltage;
            data.push([t, v]);
            var cutoff = Date.now() - 3 * 60 * 60 * 1000;
            while (data.length > 0 && data[0][0] < cutoff) data.shift();
            stats.lastVoltage = v;
            stats.totalPoints = data.length;
            if (data.length > 0) {
                var voltages = data.map(function (d) { return d[1]; });
                stats.maxVoltage = Math.max.apply(null, voltages);
                stats.minVoltage = Math.min.apply(null, voltages);
                stats.avgVoltage = voltages.reduce(function (a, b) { return a + b; }, 0) / voltages.length;
                stats.voltageRange = stats.maxVoltage - stats.minVoltage;
                var within = voltages.filter(function (v) { return Math.abs(v - STANDARD) <= TOLERANCE; }).length;
                stats.compliancePercentage = (within / voltages.length) * 100;
            }
            updateStats();
            draw();
            $('status').textContent = 'Live | ' + data.length + ' pts | ' + new Date().toLocaleTimeString();
        });

        connection.start().catch(function (err) { console.warn('SignalR:', err); });
    }

    function onModeChange() {
        var mode = $('mode').value;
        $('days-wrap').style.display = mode === 'days' ? '' : 'none';
        $('live-dot').style.display = mode === 'live' ? '' : 'none';
        clearInterval(liveInterval);
        liveInterval = null;
        fetchData();
        if (mode === 'live') liveInterval = setInterval(fetchData, 30000);
    }

    function updateStats() {
        var s = stats;
        $('s-last').textContent = s.lastVoltage ? s.lastVoltage.toFixed(1) + 'V' : '--';
        $('s-avg').textContent = s.avgVoltage ? s.avgVoltage.toFixed(1) + 'V' : '--';
        $('s-max').textContent = s.maxVoltage ? s.maxVoltage.toFixed(1) + 'V' : '--';
        $('s-min').textContent = s.minVoltage ? s.minVoltage.toFixed(1) + 'V' : '--';
        $('s-range').textContent = s.voltageRange ? s.voltageRange.toFixed(1) + 'V' : '--';
        $('s-comp').textContent = s.compliancePercentage != null ? s.compliancePercentage.toFixed(1) + '%' : '--';
        $('s-pts').textContent = s.totalPoints || data.length || '--';
        var pct = s.compliancePercentage || 0;
        $('s-comp').className = 'stat-value ' + (pct >= 95 ? 'good' : pct >= 80 ? 'warn' : 'bad');
    }

    function draw() {
        if (animFrame) cancelAnimationFrame(animFrame);
        animFrame = requestAnimationFrame(_draw);
    }

    function _draw() {
        animFrame = null;
        var w = canvas.width, h = canvas.height;
        var dpr = devicePixelRatio;
        var pad = { top: 20 * dpr, right: 50 * dpr, bottom: 30 * dpr, left: 55 * dpr };
        var plotW = w - pad.left - pad.right;
        var plotH = h - pad.top - pad.bottom;

        ctx.clearRect(0, 0, w, h);
        ctx.fillStyle = '#111';
        ctx.fillRect(0, 0, w, h);

        if (data.length < 2) {
            ctx.fillStyle = '#666';
            ctx.font = (12 * dpr) + 'px system-ui';
            ctx.textAlign = 'center';
            ctx.fillText('No data', w / 2, h / 2);
            return;
        }

        var tMin = data[0][0], tMax = data[data.length - 1][0];
        var vMin = Infinity, vMax = -Infinity;
        var allData = compareData.length > 0 ? data.concat(compareData) : data;
        for (var i = 0; i < allData.length; i++) {
            if (allData[i][1] < vMin) vMin = allData[i][1];
            if (allData[i][1] > vMax) vMax = allData[i][1];
        }
        vMin = Math.min(vMin, LOWER - 5);
        vMax = Math.max(vMax, UPPER + 5);
        var vPad = (vMax - vMin) * 0.05;
        vMin -= vPad; vMax += vPad;

        var visibleRange = (tMax - tMin) / scaleX;
        var visibleStart = tMin + panX;
        var visibleEnd = visibleStart + visibleRange;

        function tx(t) { return pad.left + ((t - visibleStart) / (visibleEnd - visibleStart)) * plotW; }
        function ty(v) { return pad.top + (1 - (v - vMin) / (vMax - vMin)) * plotH; }

        // Tolerance band
        ctx.fillStyle = 'rgba(0,200,100,0.04)';
        ctx.fillRect(pad.left, ty(UPPER), plotW, ty(LOWER) - ty(UPPER));

        // Grid
        ctx.strokeStyle = '#222';
        ctx.lineWidth = dpr;
        ctx.setLineDash([]);
        var vStep = niceStep(vMax - vMin, 6);
        ctx.font = (10 * dpr) + 'px system-ui';
        ctx.fillStyle = '#666';
        ctx.textAlign = 'right';
        ctx.textBaseline = 'middle';
        for (var v = Math.ceil(vMin / vStep) * vStep; v <= vMax; v += vStep) {
            var y = ty(v);
            ctx.beginPath(); ctx.moveTo(pad.left, y); ctx.lineTo(w - pad.right, y); ctx.stroke();
            ctx.fillText(v.toFixed(0) + 'V', pad.left - 5 * dpr, y);
        }

        ctx.textAlign = 'center';
        ctx.textBaseline = 'top';
        var tRange = visibleEnd - visibleStart;
        var tStep = niceTimeStep(tRange, plotW / (60 * dpr));
        for (var t = Math.ceil(visibleStart / tStep) * tStep; t <= visibleEnd; t += tStep) {
            var x = tx(t);
            if (x < pad.left || x > w - pad.right) continue;
            ctx.strokeStyle = '#222';
            ctx.beginPath(); ctx.moveTo(x, pad.top); ctx.lineTo(x, h - pad.bottom); ctx.stroke();
            ctx.fillStyle = '#666';
            ctx.fillText(formatTime(t, tRange), x, h - pad.bottom + 4 * dpr);
        }

        // Standard lines
        ctx.setLineDash([4 * dpr, 4 * dpr]);
        ctx.strokeStyle = '#f44'; ctx.lineWidth = 1.5 * dpr;
        ctx.beginPath(); ctx.moveTo(pad.left, ty(STANDARD)); ctx.lineTo(w - pad.right, ty(STANDARD)); ctx.stroke();
        ctx.strokeStyle = '#fa0'; ctx.lineWidth = dpr;
        ctx.beginPath(); ctx.moveTo(pad.left, ty(UPPER)); ctx.lineTo(w - pad.right, ty(UPPER)); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(pad.left, ty(LOWER)); ctx.lineTo(w - pad.right, ty(LOWER)); ctx.stroke();
        ctx.setLineDash([]);

        // Line labels
        ctx.font = (9 * dpr) + 'px system-ui';
        ctx.textAlign = 'left'; ctx.textBaseline = 'bottom';
        ctx.fillStyle = '#f44'; ctx.fillText('230V', w - pad.right + 3 * dpr, ty(STANDARD));
        ctx.fillStyle = '#fa0'; ctx.fillText(UPPER + 'V', w - pad.right + 3 * dpr, ty(UPPER));
        ctx.fillText(LOWER + 'V', w - pad.right + 3 * dpr, ty(LOWER));

        // Data lines
        if (compareData.length > 1) drawLine(compareData, visibleStart, visibleEnd, tx, ty, 'rgba(255,80,80,0.6)', 1.5 * dpr);
        drawLine(data, visibleStart, visibleEnd, tx, ty, '#0cf', 2 * dpr);

        // Min/Max markers
        if (data.length > 1) {
            var maxIdx = 0, minIdx = 0;
            for (var i = 1; i < data.length; i++) {
                if (data[i][1] > data[maxIdx][1]) maxIdx = i;
                if (data[i][1] < data[minIdx][1]) minIdx = i;
            }
            drawMarker(tx, ty, data[maxIdx], '#f44', 'Max ' + data[maxIdx][1].toFixed(1) + 'V', dpr, pad, w, true);
            drawMarker(tx, ty, data[minIdx], '#fa0', 'Min ' + data[minIdx][1].toFixed(1) + 'V', dpr, pad, w, false);
        }

        // Border
        ctx.strokeStyle = '#333'; ctx.lineWidth = dpr;
        ctx.strokeRect(pad.left, pad.top, plotW, plotH);

        // Cache transforms for hover
        plotState = { pad: pad, plotW: plotW, plotH: plotH, visibleStart: visibleStart, visibleEnd: visibleEnd, vMin: vMin, vMax: vMax, w: w, h: h, dpr: dpr };

        // Draw tooltip crosshair
        if (hoverIdx >= 0 && hoverIdx < data.length) {
            var pt = data[hoverIdx];
            var px = tx(pt[0]), py = ty(pt[1]);
            // Vertical crosshair
            ctx.strokeStyle = 'rgba(255,255,255,0.2)';
            ctx.lineWidth = dpr;
            ctx.setLineDash([2 * dpr, 2 * dpr]);
            ctx.beginPath(); ctx.moveTo(px, pad.top); ctx.lineTo(px, h - pad.bottom); ctx.stroke();
            // Horizontal crosshair
            ctx.beginPath(); ctx.moveTo(pad.left, py); ctx.lineTo(w - pad.right, py); ctx.stroke();
            ctx.setLineDash([]);
            // Dot
            ctx.beginPath(); ctx.arc(px, py, 4 * dpr, 0, Math.PI * 2);
            ctx.fillStyle = '#0cf'; ctx.fill();
            ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5 * dpr; ctx.stroke();
            // Tooltip box
            var d = new Date(pt[0]);
            var line1 = d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }) + '  ' + d.toLocaleTimeString();
            var line2 = pt[1].toFixed(2) + 'V';
            ctx.font = 'bold ' + (11 * dpr) + 'px system-ui';
            var tw1 = ctx.measureText(line1).width;
            ctx.font = (13 * dpr) + 'px system-ui';
            var tw2 = ctx.measureText(line2).width;
            var boxW = Math.max(tw1, tw2) + 16 * dpr;
            var boxH = 38 * dpr;
            var bx = px + 12 * dpr;
            var by = py - boxH - 8 * dpr;
            // Keep tooltip on screen
            if (bx + boxW > w - pad.right) bx = px - boxW - 12 * dpr;
            if (by < pad.top) by = py + 12 * dpr;
            ctx.fillStyle = 'rgba(30,30,30,0.92)';
            ctx.strokeStyle = '#555';
            ctx.lineWidth = dpr;
            ctx.beginPath();
            ctx.roundRect(bx, by, boxW, boxH, 4 * dpr);
            ctx.fill(); ctx.stroke();
            ctx.fillStyle = '#aaa';
            ctx.font = (10 * dpr) + 'px system-ui';
            ctx.textAlign = 'left'; ctx.textBaseline = 'top';
            ctx.fillText(line1, bx + 8 * dpr, by + 5 * dpr);
            ctx.fillStyle = '#0cf';
            ctx.font = 'bold ' + (12 * dpr) + 'px system-ui';
            ctx.fillText(line2, bx + 8 * dpr, by + 20 * dpr);
        }
    }

    function drawLine(pts, tStart, tEnd, tx, ty, color, lw) {
        if (pts.length < 2) return;
        ctx.strokeStyle = color;
        ctx.lineWidth = lw;
        ctx.beginPath();
        var started = false;
        var step = Math.max(1, Math.floor(pts.length / 2000));
        for (var i = 0; i < pts.length; i += step) {
            var t = pts[i][0], v = pts[i][1];
            if (t < tStart || t > tEnd) { started = false; continue; }
            var x = tx(t), y = ty(v);
            if (!started) { ctx.moveTo(x, y); started = true; } else ctx.lineTo(x, y);
        }
        ctx.stroke();
    }

    function drawMarker(tx, ty, pt, color, label, dpr, pad, w, above) {
        var px = tx(pt[0]), py = ty(pt[1]);
        // Diamond marker
        ctx.beginPath();
        ctx.moveTo(px, py - 6 * dpr);
        ctx.lineTo(px + 5 * dpr, py);
        ctx.lineTo(px, py + 6 * dpr);
        ctx.lineTo(px - 5 * dpr, py);
        ctx.closePath();
        ctx.fillStyle = color; ctx.fill();
        ctx.strokeStyle = '#fff'; ctx.lineWidth = 1.5 * dpr; ctx.stroke();
        // Label
        ctx.font = 'bold ' + (9 * dpr) + 'px system-ui';
        ctx.textAlign = 'center'; ctx.textBaseline = above ? 'bottom' : 'top';
        ctx.fillStyle = color;
        ctx.fillText(label, px, py + (above ? -9 : 9) * dpr);
    }

    // Interaction
    function onMouseDown(e) { dragging = true; dragStartX = e.clientX; dragStartPan = panX; canvas.style.cursor = 'grabbing'; hoverIdx = -1; }
    function onMouseMove(e) {
        if (dragging && data.length >= 2) {
            var dx = e.clientX - dragStartX;
            var tRange = (data[data.length - 1][0] - data[0][0]) / scaleX;
            var pxRange = canvas.getBoundingClientRect().width;
            panX = dragStartPan - (dx / pxRange) * tRange;
            panX = Math.max(0, Math.min(panX, (data[data.length - 1][0] - data[0][0]) - tRange));
            draw();
            return;
        }
        if (!plotState || data.length < 2) return;
        var rect = canvas.getBoundingClientRect();
        var mx = (e.clientX - rect.left) * devicePixelRatio;
        var my = (e.clientY - rect.top) * devicePixelRatio;
        var s = plotState;
        if (mx < s.pad.left || mx > s.w - s.pad.right || my < s.pad.top || my > s.h - s.pad.bottom) {
            if (hoverIdx !== -1) { hoverIdx = -1; draw(); }
            return;
        }
        // Convert pixel x to time
        var t = s.visibleStart + ((mx - s.pad.left) / s.plotW) * (s.visibleEnd - s.visibleStart);
        // Binary search for nearest point
        var lo = 0, hi = data.length - 1;
        while (lo < hi) {
            var mid = (lo + hi) >> 1;
            if (data[mid][0] < t) lo = mid + 1; else hi = mid;
        }
        if (lo > 0 && Math.abs(data[lo - 1][0] - t) < Math.abs(data[lo][0] - t)) lo--;
        hoverIdx = lo;
        draw();
    }
    function onMouseUp() { dragging = false; canvas.style.cursor = ''; }
    function onMouseLeave() { dragging = false; canvas.style.cursor = ''; hoverIdx = -1; draw(); }
    function onWheel(e) {
        e.preventDefault();
        if (data.length < 2) return;
        var factor = e.deltaY > 0 ? 0.85 : 1.18;
        var newScale = Math.max(1, Math.min(scaleX * factor, 100));
        if (newScale === scaleX) return;
        var rect = canvas.getBoundingClientRect();
        var mouseRatio = (e.clientX - rect.left) / rect.width;
        var tTotal = data[data.length - 1][0] - data[0][0];
        var oldVisible = tTotal / scaleX;
        var newVisible = tTotal / newScale;
        panX += (oldVisible - newVisible) * mouseRatio;
        scaleX = newScale;
        panX = Math.max(0, Math.min(panX, tTotal - newVisible));
        draw();
    }

    var lastTouch = null;
    function onTouchStart(e) {
        if (e.touches.length === 1) { e.preventDefault(); dragging = true; dragStartX = e.touches[0].clientX; dragStartPan = panX; }
        lastTouch = e.touches.length === 2 ? Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY) : null;
    }
    function onTouchMove(e) {
        if (e.touches.length === 1 && dragging) {
            e.preventDefault();
            var dx = e.touches[0].clientX - dragStartX;
            var tRange = (data[data.length - 1][0] - data[0][0]) / scaleX;
            panX = dragStartPan - (dx / canvas.getBoundingClientRect().width) * tRange;
            panX = Math.max(0, panX); draw();
        } else if (e.touches.length === 2 && lastTouch) {
            e.preventDefault();
            var dist = Math.hypot(e.touches[0].clientX - e.touches[1].clientX, e.touches[0].clientY - e.touches[1].clientY);
            scaleX = Math.max(1, Math.min(scaleX * (dist / lastTouch), 100));
            lastTouch = dist; draw();
        }
    }

    function resetZoom() { panX = 0; scaleX = 1; draw(); }
    function showLoader(show) { $('loader').classList.toggle('hidden', !show); }
    function setError(message) {
        var banner = $('error-banner');
        banner.textContent = message || '';
        banner.classList.toggle('visible', !!message);
    }

    function niceStep(range, ticks) {
        var rough = range / ticks, mag = Math.pow(10, Math.floor(Math.log10(rough))), n = rough / mag;
        return (n < 1.5 ? 1 : n < 3 ? 2 : n < 7 ? 5 : 10) * mag;
    }
    function niceTimeStep(ms, ticks) {
        var steps = [60000, 300000, 600000, 1800000, 3600000, 7200000, 14400000, 28800000, 43200000, 86400000];
        var rough = ms / ticks;
        for (var i = 0; i < steps.length; i++) { if (steps[i] >= rough) return steps[i]; }
        return 86400000;
    }
    function formatTime(ms, range) {
        var d = new Date(ms);
        if (range > 172800000) return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
        if (range > 86400000) return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) + ' ' + d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
