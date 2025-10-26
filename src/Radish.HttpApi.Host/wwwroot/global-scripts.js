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
    }
  });
})();
