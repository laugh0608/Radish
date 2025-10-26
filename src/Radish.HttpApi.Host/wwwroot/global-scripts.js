/* Your Global Scripts */

(function(){
  // --- Keys ---
  var KEY_THEME   = 'radish.theme';           // 'light' | 'dark'
  var KEY_DENSITY = 'radish.density';         // 'auto' | 'comfortable' | 'compact'
  var KEY_ORDER   = 'radish.order';           // [clientId]
  var KEY_FAVS    = 'radish.favs';            // [clientId]
  var KEY_RECENT  = 'radish.recent';          // [{id,title,href}]
  var KEY_HIDDEN  = 'radish.hidden';          // [clientId]
  var KEY_META    = 'radish.meta';            // {id:{title,href}}

  var root = document.documentElement;

  // --- Theme ---
  function applyTheme(t){
    root.classList.remove('theme-light','theme-dark');
    root.classList.add(t === 'dark' ? 'theme-dark' : 'theme-light');
  }
  var theme = localStorage.getItem(KEY_THEME);
  if(!theme){ var prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; theme = prefersDark ? 'dark' : 'light'; }
  applyTheme(theme);

  // --- Density ---
  function computeAutoDensity(){ var w = Math.max(document.documentElement.clientWidth, window.innerWidth||0); return w <= 576 ? 'compact' : 'comfortable'; }
  function applyDensityClass(d){ root.classList.toggle('density-compact', d === 'compact'); }
  function applyDensity(mode){ applyDensityClass(mode === 'auto' ? computeAutoDensity() : mode); }
  var densityMode = localStorage.getItem(KEY_DENSITY) || 'comfortable';
  applyDensity(densityMode);
  window.addEventListener('resize', function(){ if(densityMode === 'auto') applyDensity('auto'); });

  // --- Tiny store helpers ---
  function sget(key, def){ try{ var v = localStorage.getItem(key); return v ? JSON.parse(v) : def; }catch{ return def; } }
  function sput(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch{} }

  // Specific getters
  function getOrder(){ return sget(KEY_ORDER, []); }
  function setOrder(v){ sput(KEY_ORDER, v); }
  function getFavs(){ return sget(KEY_FAVS, []); }
  function setFavs(v){ sput(KEY_FAVS, v); }
  function getRecent(){ return sget(KEY_RECENT, []); }
  function setRecent(v){ sput(KEY_RECENT, v); }
  function getHidden(){ return sget(KEY_HIDDEN, []); }
  function setHidden(v){ sput(KEY_HIDDEN, v); }
  function getMeta(){ return sget(KEY_META, {}); }
  function setMeta(v){ sput(KEY_META, v); }

  // --- DOM Ready ---
  window.addEventListener('DOMContentLoaded', function(){
    // Theme toggles
    var themeBtn = document.getElementById('themeToggle');
    if(themeBtn) themeBtn.addEventListener('click', function(){ theme = theme === 'dark' ? 'light' : 'dark'; applyTheme(theme); localStorage.setItem(KEY_THEME, theme); });
    var themeBtnM = document.getElementById('themeToggleMobile');
    if(themeBtnM) themeBtnM.addEventListener('click', function(){ theme = theme === 'dark' ? 'light' : 'dark'; applyTheme(theme); localStorage.setItem(KEY_THEME, theme); });

    // Density menu (auto/comfortable/compact)
    document.querySelectorAll('.density-item').forEach(function(item){
      item.addEventListener('click', function(){ densityMode = item.getAttribute('data-density') || 'comfortable'; localStorage.setItem(KEY_DENSITY, densityMode); applyDensity(densityMode); });
    });

    // Grid + cards
    var grid = document.getElementById('appsGrid'); if(!grid) return;

    // Meta map for all visible cards
    var meta = getMeta();
    Array.from(grid.querySelectorAll('.radish-app-card')).forEach(function(a){
      var col = a.closest('.col'); var id = col ? (col.getAttribute('data-client-id')||'') : '';
      if(!id) return;
      var title = a.getAttribute('data-title') || (a.querySelector('.h5') ? a.querySelector('.h5').textContent.trim() : id);
      var href  = a.getAttribute('data-href') || a.getAttribute('href') || '';
      meta[id] = { title: title, href: href };
    });
    setMeta(meta);

    // Apply saved order first
    (function applyOrder(){
      var order = getOrder(); if(!order.length) return;
      var map = {}; order.forEach(function(id,i){ map[id]=i; });
      var items = Array.from(grid.querySelectorAll(':scope > .col'));
      items.sort(function(a,b){
        var ia = map[a.getAttribute('data-client-id')||''];
        var ib = map[b.getAttribute('data-client-id')||''];
        ia = (typeof ia==='number')? ia : 9999; ib = (typeof ib==='number')? ib : 9999;
        if(ia!==ib) return ia-ib;
        return (a.textContent||'').localeCompare((b.textContent||''));
      });
      items.forEach(function(el){ grid.appendChild(el); });
    })();

    // Hidden sync
    function syncHidden(){
      var hidden = getHidden();
      Array.from(grid.querySelectorAll(':scope > .col')).forEach(function(col){
        var id = col.getAttribute('data-client-id')||'';
        col.classList.toggle('d-none', hidden.indexOf(id) >= 0);
      });
    }
    syncHidden();

    // Favorites init + toggle
    var favs = getFavs();
    function toggleFav(id){ var pos = favs.indexOf(id); if(pos>=0) favs.splice(pos,1); else favs.push(id); setFavs(favs); return favs; }
    grid.querySelectorAll('.fav-toggle').forEach(function(el){
      var id = el.getAttribute('data-client-id')||'';
      if(favs.indexOf(id) >= 0){ el.classList.add('is-fav'); el.textContent = '★'; } else { el.textContent = '☆'; }
      el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation();
        var now = toggleFav(id);
        if(now.indexOf(id) >= 0){ el.classList.add('is-fav'); el.textContent = '★'; } else { el.classList.remove('is-fav'); el.textContent = '☆'; }
        try{
          var items = Array.from(grid.querySelectorAll(':scope > .col'));
          var order = getOrder(); var map = {}; order.forEach(function(x,i){ map[x]=i; });
          items.sort(function(a,b){
            var ia = now.indexOf(a.getAttribute('data-client-id')||'') >= 0 ? 0 : 1;
            var ib = now.indexOf(b.getAttribute('data-client-id')||'') >= 0 ? 0 : 1;
            if(ia !== ib) return ia - ib;
            var oa = map[a.getAttribute('data-client-id')||'']; var ob = map[b.getAttribute('data-client-id')||''];
            oa = (typeof oa==='number')?oa:9999; ob = (typeof ob==='number')?ob:9999;
            if(oa!==ob) return oa-ob;
            return (a.textContent||'').localeCompare((b.textContent||''));
          });
          items.forEach(function(n){ grid.appendChild(n); });
        }catch{}
      });
    });

    // Copy link
    grid.querySelectorAll('.copy-link').forEach(function(el){ el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); var href = el.getAttribute('data-href')||''; if(navigator.clipboard){ navigator.clipboard.writeText(href).then(function(){ if(window.abp&&abp.notify){ abp.notify.info('已复制链接'); } else { console.info('已复制链接: '+href); } }).catch(function(){ alert('复制失败'); }); } else { var t=document.createElement('textarea'); t.value=href; document.body.appendChild(t); t.select(); try{ document.execCommand('copy'); if(window.abp&&abp.notify){ abp.notify.info('已复制链接'); } } finally { document.body.removeChild(t); } } }); });

    // Health check
    grid.querySelectorAll('.health-check').forEach(function(el){ el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); var origin = el.getAttribute('data-health-origin')||''; if(!origin){ if(window.abp&&abp.notify) abp.notify.warn('未配置健康检查'); return; } var url = origin.replace(/\/$/,'') + '/health-status'; fetch(url,{method:'GET'}).then(function(r){ if(!r.ok) throw new Error('HTTP '+r.status); return r.json(); }).then(function(data){ var status=(data&&data.status)||'Unknown'; if(window.abp&&abp.notify){ if((status||'').toLowerCase()==='healthy') abp.notify.success('健康: '+status); else abp.notify.warn('健康: '+status); } else { alert('健康状态: '+status); } }).catch(function(){ if(window.abp&&abp.notify) abp.notify.error('检查失败'); else alert('检查失败'); }); }); });

    // Recent
    function touchRecent(item){ var list=getRecent(); list=list.filter(function(x){return x&&x.id!==item.id;}); list.unshift(item); if(list.length>6) list=list.slice(0,6); setRecent(list); }
    grid.querySelectorAll('.radish-app-card').forEach(function(a){ a.addEventListener('click', function(){ var parent=a.closest('.col'); var id=parent?(parent.getAttribute('data-client-id')||''):''; var title=a.getAttribute('data-title')||(a.querySelector('.h5')?a.querySelector('.h5').textContent.trim():id); var href=a.getAttribute('data-href')||a.getAttribute('href')||''; touchRecent({id:id, href:href, title:title}); }); });
    function renderRecent(){ var hidden=getHidden(); var recent=getRecent().filter(function(x){ return x && hidden.indexOf(x.id) < 0; }); var row=document.getElementById('recentRow'); if(!row) return; if(!recent.length){ row.classList.add('d-none'); return; } row.classList.remove('d-none'); row.innerHTML=''; recent.forEach(function(r){ var col=document.createElement('div'); col.className='col'; col.innerHTML='\n<a href="'+(r.href||'#')+'" target="_blank" class="radish-app-card d-block h-100 text-reset">\n  <div class="card h-100 border-0 shadow-sm radish-card">\n    <div class="card-body d-flex align-items-start gap-3">\n      <div class="flex-grow-1">\n        <div class="h6 mb-1">'+(r.title||r.id||'App')+'</div>\n        <div class="text-muted small text-truncate">'+(r.href||'')+'</div>\n      </div>\n    </div>\n  </div>\n</a>'; row.appendChild(col); }); }
    renderRecent();

    // Hide toggle & manage
    grid.querySelectorAll('.hide-toggle').forEach(function(el){ el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); var id=el.getAttribute('data-client-id')||''; var arr=getHidden(); if(arr.indexOf(id)<0) arr.push(id); setHidden(arr); syncHidden(); renderRecent(); if(window.abp&&abp.notify) abp.notify.info('已隐藏'); }); });

    function openHiddenManage(){ try{ var list=document.getElementById('hiddenList'); if(!list) return; var arr=getHidden(); var meta=getMeta(); list.innerHTML=''; if(!arr.length){ list.innerHTML='<div class="text-muted">暂无隐藏项</div>'; } arr.forEach(function(id){ var m=meta[id]||{title:id, href:''}; var item=document.createElement('div'); item.className='list-group-item d-flex justify-content-between align-items-center'; item.innerHTML='<div><div class="fw-semibold">'+(m.title||id)+'</div><div class="text-muted small">'+(m.href||'')+'</div></div><button type="button" class="btn btn-sm btn-outline-primary unhide" data-id="'+id+'">显示</button>'; list.appendChild(item); }); list.querySelectorAll('.unhide').forEach(function(btn){ btn.addEventListener('click', function(){ var id=btn.getAttribute('data-id')||''; var a=getHidden().filter(function(x){return x!==id;}); setHidden(a); syncHidden(); renderRecent(); btn.closest('.list-group-item').remove(); }); }); var modalEl=document.getElementById('hiddenManageModal'); if(!modalEl) return; var mdl=new bootstrap.Modal(modalEl); mdl.show(); }catch(e){ console.warn(e); } }
    var hBtn=document.getElementById('hiddenManageBtn'); if(hBtn) hBtn.addEventListener('click', openHiddenManage);
    var hBtnM=document.getElementById('hiddenManageBtnMobile'); if(hBtnM) hBtnM.addEventListener('click', openHiddenManage);

    // Edit mode (drag & drop)
    var editBtn = document.getElementById('editToggle');
    var editBtnM = document.getElementById('editToggleMobile');
    function readIds(container){ return Array.from(container.querySelectorAll(':scope > .col')).map(function(x){ return x.getAttribute('data-client-id')||'';}); }
    function setEditMode(on){ document.body.classList.toggle('edit-mode', !!on); grid.querySelectorAll(':scope > .col').forEach(function(col){ col.setAttribute('draggable', on? 'true':'false'); }); if(editBtn) editBtn.textContent = on? '完成':'编辑'; }
    var editing=false; setEditMode(editing);
    function toggleEdit(){ editing=!editing; setEditMode(editing); }
    if(editBtn) editBtn.addEventListener('click', toggleEdit);
    if(editBtnM) editBtnM.addEventListener('click', toggleEdit);
    var dragSrc=null;
    grid.addEventListener('dragstart', function(e){ var t=e.target.closest('.col'); if(!t||!editing) return; dragSrc=t; t.classList.add('edit-dragging'); e.dataTransfer.effectAllowed='move'; });
    grid.addEventListener('dragover', function(e){ if(editing){ e.preventDefault(); e.dataTransfer.dropEffect='move'; }});
    grid.addEventListener('drop', function(e){ if(!editing) return; e.preventDefault(); var tgt=e.target.closest('.col'); if(!tgt||!dragSrc||tgt===dragSrc) return; var rect=tgt.getBoundingClientRect(); var before=(e.clientY-rect.top) < rect.height/2; if(before) grid.insertBefore(dragSrc, tgt); else grid.insertBefore(dragSrc, tgt.nextSibling); setOrder(readIds(grid)); });
    grid.addEventListener('dragend', function(e){ var t=e.target.closest('.col'); if(t) t.classList.remove('edit-dragging'); dragSrc=null; });
  });
})();

