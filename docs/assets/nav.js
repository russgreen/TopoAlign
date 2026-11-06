/**
 * Single source of truth for the documentation navigation.
 * Renders the sidebar (<nav class="side-nav">) and the previous/next pager
 * (<nav class="pager">) on every page. To add, rename or reorder a command page,
 * edit the COMMANDS list below - no other page needs to change.
 *
 * Include with a plain (non-deferred) script tag after the two empty <nav> elements:
 *   <script src="../assets/nav.js"></script>     (command pages)
 *   <script src="./assets/nav.js"></script>      (index.html)
 */
(function () {
    // Site root (the docs folder), resolved from where this script was loaded.
    var script = document.currentScript;
    var root = new URL('../', script.src).href;

    var GETTING_STARTED = [
        { title: 'System requirements', href: 'index.html#requirements' },
        { title: 'Installation', href: 'index.html#installation' },
        { title: 'Quick start', href: 'index.html#quick-start' }
    ];

    // Order here is the order of the sidebar and of the previous/next pager.
    var COMMANDS = [
        { title: 'Align to Element', href: 'Commands/CommandAlignTopo.html' },
        { title: 'Align to Topo', href: 'Commands/CommandAlignFloor.html' },
        { title: 'Align Topo Edges', href: 'Commands/CommandAlignTopoEdges.html' },
        { title: 'Points from Lines', href: 'Commands/CommandPointsOnSurface.html' },
        { title: 'Points Along Contours', href: 'Commands/CommandPointsAlongContours.html' },
        { title: 'Points At Intersection', href: 'Commands/CommandPointsAtIntersection.html' },
        { title: 'Reset Topo Region', href: 'Commands/CommandResetTopoRegion.html' },
        { title: 'Create Alignment', href: 'Commands/CommandCreateAlignment.html' },
        { title: 'Edit Alignment', href: 'Commands/CommandEditAlignment.html' },
        { title: 'Edit Profile', href: 'Commands/CommandEditProfile.html' },
        { title: 'Station Labels', href: 'Commands/CommandStationLabels.html' },
        { title: 'Sweep Along Alignment', href: 'Commands/CommandSweepAlignment.html' },
        { title: 'Sweep to Toposolid', href: 'Commands/CommandSweepToToposolid.html' },
        { title: 'Grade to Surface', href: 'Commands/CommandGradeToSurface.html' },
        { title: 'Dropped Kerb Ramp', href: 'Commands/CommandDroppedKerbRamp.html' }
    ];

    var REFERENCE = [
        { title: 'Configuration & paths', href: 'index.html#config' },
        { title: 'Logging', href: 'index.html#logging' },
        { title: 'Support', href: 'index.html#support' },
        { title: "What's new", href: 'index.html#whats-new' }
    ];

    var OVERVIEW = { title: 'Overview', href: 'index.html' };

    function esc(text) {
        return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function url(item) {
        return root + item.href;
    }

    // Path (without query/hash) of the page being viewed, to flag the current command.
    var here = location.pathname;
    function isCurrent(item) {
        return new URL(url(item)).pathname === here && item.href.indexOf('#') < 0;
    }

    function section(heading, items) {
        var links = items.map(function (item) {
            var current = isCurrent(item) ? ' aria-current="page"' : '';
            return '<li><a href="' + esc(url(item)) + '"' + current + '>' + esc(item.title) + '</a></li>';
        }).join('');
        return '<section><h2>' + esc(heading) + '</h2><ul>' + links + '</ul></section>';
    }

    var sideNav = document.querySelector('nav.side-nav');
    if (sideNav) {
        sideNav.innerHTML =
            section('Getting started', GETTING_STARTED) +
            section('Commands', COMMANDS) +
            section('Reference', REFERENCE);
    }

    var pager = document.querySelector('nav.pager');
    if (pager) {
        // The overview page sits in front of the first command.
        var pages = [OVERVIEW].concat(COMMANDS);
        var index = -1;
        pages.forEach(function (page, i) {
            if (new URL(url(page)).pathname === here ||
                (page === OVERVIEW && here === new URL(root).pathname)) {
                index = i;
            }
        });

        var html = '';
        if (index > 0) {
            html += '<a class="pager-prev" href="' + esc(url(pages[index - 1])) + '">&larr; ' + esc(pages[index - 1].title) + '</a>';
        }
        if (index >= 0 && index < pages.length - 1) {
            html += '<a class="pager-next" href="' + esc(url(pages[index + 1])) + '">' + esc(pages[index + 1].title) + ' &rarr;</a>';
        }
        pager.innerHTML = html;
    }
})();
