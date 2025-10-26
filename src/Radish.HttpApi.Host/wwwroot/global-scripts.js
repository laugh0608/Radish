/* Your Global Scripts */

(function(){
  // --- Theme toggle ---
  var KEY_THEME = 'radish.theme';
  var root = document.documentElement;
  function applyTheme(t){
    root.classList.remove('theme-light','theme-dark');
    root.classList.add(t === 'dark' ? 'theme-dark' : 'theme-light');
  }
  var pref = localStorage.getItem(KEY_THEME);
  if(!pref){
    // 默认浅色；若用户系统是深色且未设置过，可按系统
    var prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    pref = prefersDark ? 'dark' : 'light';
  }
  applyTheme(pref);
  window.addEventListener('DOMContentLoaded', function(){
    var btn = document.getElementById('themeToggle');
    if(btn){
      btn.addEventListener('click', function(){
        var cur = root.classList.contains('theme-dark') ? 'dark' : 'light';
        var next = cur === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        localStorage.setItem(KEY_THEME, next);
      });
    }
    var btnM = document.getElementById('themeToggleMobile');
    if(btnM){ btnM.addEventListener('click', function(){
      var cur = root.classList.contains('theme-dark') ? 'dark' : 'light';
      var next = cur === 'dark' ? 'light' : 'dark';
      applyTheme(next); localStorage.setItem(KEY_THEME, next);
    }); }

    // --- Favorites ---
    var KEY_FAVS = 'radish.favs';
    function getFavs(){ try{ return JSON.parse(localStorage.getItem(KEY_FAVS) || '[]'); }catch{ return []; } }
    function setFavs(arr){ try{ localStorage.setItem(KEY_FAVS, JSON.stringify(arr||[])); }catch{} }
    function toggleFav(id){
      var favs = getFavs();
      var i = favs.indexOf(id);
      if(i >= 0) favs.splice(i,1); else favs.push(id);
      setFavs(favs);
      return favs;
    }

    var grid = document.getElementById('appsGrid');
    if(grid){
      // 初始化收藏状态
      var favs = getFavs();
      grid.querySelectorAll('.fav-toggle').forEach(function(el){
        var id = el.getAttribute('data-client-id') || '';
        if(favs.indexOf(id) >= 0){ el.classList.add('is-fav'); el.textContent = '★'; }
        else { el.textContent = '☆'; }
        el.addEventListener('click', function(e){
          e.preventDefault(); e.stopPropagation();
          var now = toggleFav(id);
          if(now.indexOf(id) >= 0){ el.classList.add('is-fav'); el.textContent = '★'; } else { el.classList.remove('is-fav'); el.textContent = '☆'; }
          // 重新排序：收藏置顶
          try{
            var items = Array.from(grid.querySelectorAll(':scope > .col'));
            items.sort(function(a,b){
              var ia = now.indexOf(a.getAttribute('data-client-id')||'') >= 0 ? 0 : 1;
              var ib = now.indexOf(b.getAttribute('data-client-id')||'') >= 0 ? 0 : 1;
              if(ia !== ib) return ia - ib;
              return (a.textContent||'').localeCompare(b.textContent||'');
            });
            items.forEach(function(n){ grid.appendChild(n); });
          }catch{}
        });
      });

      // 复制链接
      grid.querySelectorAll('.copy-link').forEach(function(el){
        el.addEventListener('click', function(e){
          e.preventDefault(); e.stopPropagation();
          var href = el.getAttribute('data-href') || '';
          if(navigator.clipboard){
            navigator.clipboard.writeText(href).then(function(){
              if(window.abp && abp.notify){ abp.notify.info('已复制链接'); } else { console.info('已复制链接: ' + href); }
            }).catch(function(){ alert('复制失败'); });
          }else{
            // 兼容：无 clipboard API
            var t = document.createElement('textarea'); t.value = href; document.body.appendChild(t); t.select();
            try{ document.execCommand('copy'); if(window.abp&&abp.notify){ abp.notify.info('已复制链接'); } } finally { document.body.removeChild(t); }
          }
        });
      });

      // 最近访问：记录并渲染置顶区
      var KEY_RECENT = 'radish.recent';
      function getRecent(){ try{return JSON.parse(localStorage.getItem(KEY_RECENT)||'[]');}catch{return [];} }
      function setRecent(arr){ try{localStorage.setItem(KEY_RECENT, JSON.stringify(arr||[]));}catch{} }
      function touchRecent(item){
        var list = getRecent();
        // item: {id, href, title}
        list = list.filter(function(x){ return x && x.id !== item.id; });
        list.unshift(item);
        if(list.length>6) list = list.slice(0,6);
        setRecent(list);
      }

      grid.querySelectorAll('.radish-app-card').forEach(function(a){
        a.addEventListener('click', function(){
          var parent = a.closest('.col');
          var id = parent ? (parent.getAttribute('data-client-id')||'') : '';
          var title = a.querySelector('.h5') ? a.querySelector('.h5').textContent.trim() : id;
          var href = a.getAttribute('data-href') || a.getAttribute('href') || '';
          touchRecent({id:id, href:href, title:title});
        });
      });

      function renderRecent(){
        var recent = getRecent(); var row = document.getElementById('recentRow');
        if(!row) return; if(!recent.length){ row.classList.add('d-none'); return; }
        row.classList.remove('d-none'); row.innerHTML = '';
        recent.forEach(function(r){
          var col = document.createElement('div'); col.className = 'col';
          col.innerHTML = '\n<a href="'+(r.href||'#')+'" target="_blank" class="radish-app-card d-block h-100 text-reset">\n  <div class="card h-100 border-0 shadow-sm radish-card">\n    <div class="card-body d-flex align-items-start gap-3">\n      <div class="flex-grow-1">\n        <div class="h6 mb-1">'+(r.title||r.id||'App')+'</div>\n        <div class="text-muted small text-truncate">'+(r.href||'')+'</div>\n      </div>\n    </div>\n  </div>\n</a>';
          row.appendChild(col);
        });
      }
      renderRecent();
    }
  });
})();
