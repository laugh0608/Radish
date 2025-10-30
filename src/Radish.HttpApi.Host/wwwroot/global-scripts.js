/* Your Global Scripts */

(function(){
  // --- Keys ---
  var KEY_THEME   = 'radish.theme';           // 'light' | 'dark'
  var KEY_DENSITY = 'radish.density';         // 'auto' | 'comfortable' | 'compact'
  var KEY_ORDER   = 'radish.order';           // [clientId]
  var KEY_FAVS    = 'radish.favs';            // [clientId]
  var KEY_RECENT  = 'radish.recent';          // [{id,title,href}]
  var KEY_RECENT_ONLYFAVS = 'radish.recent.onlyFavs'; // boolean
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
    // Localization helper
    var lres = (window.abp && abp.localization && abp.localization.getResource) ? abp.localization.getResource('Radish') : function(s){ return s; };
    function L(key){
      try{
        var args = Array.prototype.slice.call(arguments,1);
        var t = lres ? lres(key) : key;
        if(args.length){ return t.replace(/\{(\d+)\}/g, function(_,i){ return (args[i]!==undefined? args[i] : '{'+i+'}'); }); }
        return t;
      }catch{ return key; }
    }
    // Theme toggles
    var themeBtn = document.getElementById('themeToggle');
    if(themeBtn) themeBtn.addEventListener('click', function(){ theme = theme === 'dark' ? 'light' : 'dark'; applyTheme(theme); localStorage.setItem(KEY_THEME, theme); });
    var themeBtnM = document.getElementById('themeToggleMobile');
    if(themeBtnM) themeBtnM.addEventListener('click', function(){ theme = theme === 'dark' ? 'light' : 'dark'; applyTheme(theme); localStorage.setItem(KEY_THEME, theme); });

    // Fix: Hero 溢出裁剪导致下拉被遮挡，监听下拉开合切换 overflow
    try{
      var hero = document.querySelector('.radish-hero');
      if(hero){
        document.querySelectorAll('.radish-hero .dropdown, .radish-hero abp-dropdown').forEach(function(el){
          el.addEventListener('shown.bs.dropdown', function(){ hero.classList.add('dropdown-open'); });
          el.addEventListener('hidden.bs.dropdown', function(){ hero.classList.remove('dropdown-open'); });
        });
      }
    }catch{}

    // Density menu (auto/comfortable/compact)
    function setDensityActive(mode){
      try{
        document.querySelectorAll('.density-item').forEach(function(el){
          var m = el.getAttribute('data-density') || '';
          if(m === mode){ el.classList.add('active'); el.setAttribute('aria-pressed','true'); }
          else { el.classList.remove('active'); el.setAttribute('aria-pressed','false'); }
        });
      }catch{}
    }
    document.querySelectorAll('.density-item').forEach(function(item){
      item.addEventListener('click', function(){
        densityMode = item.getAttribute('data-density') || 'comfortable';
        localStorage.setItem(KEY_DENSITY, densityMode);
        applyDensity(densityMode);
        setDensityActive(densityMode);
      });
    });
    // 初始化当前选中态
    setDensityActive(densityMode);

    // Grid + cards
    var grid = document.getElementById('appsGrid'); if(!grid) return;
    
    // 构建“更多”菜单：把部分按钮折叠为下拉项
    function setupToolsMore(tools){
      try{
        if(!tools) return;
        var more = tools.querySelector('.tools-more');
        if(!more){
          more = document.createElement('div');
          more.className = 'dropdown tools-more';
          more.innerHTML = '<button class="btn btn-sm btn-light-subtle border-0 dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false">⋯</button><ul class="dropdown-menu dropdown-menu-end"></ul>';
          tools.appendChild(more);
        }

        var fav    = tools.querySelector('.fav-toggle');
        var copy   = tools.querySelector('.copy-link');
        var health = tools.querySelector('.health-check');
        var hideB  = tools.querySelector('.hide-toggle');
        var menu   = more.querySelector('.dropdown-menu');
        var card   = tools.closest('.card');
        var title  = card ? card.querySelector('.app-title') : null;

        function rebuildMenu(items){
          if(!menu) return; menu.innerHTML='';
          items.forEach(function(it){
            var li=document.createElement('li');
            var a=document.createElement('button'); a.type='button'; a.className='dropdown-item'; a.textContent=it.text;
            a.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); it.btn.click(); });
            li.appendChild(a); menu.appendChild(li);
          });
        }

        function update(){
          // 始终展示四个工具按钮，隐藏“更多”
          [fav, copy, health, hideB].forEach(function(x){ if(x) x.classList.remove('d-none'); });
          rebuildMenu([]);
          if(more) more.classList.add('d-none');
        }

        // 初始化与监听尺寸变化
        update();
        if(window.ResizeObserver && card){ var ro=new ResizeObserver(update); ro.observe(card); }
        window.addEventListener('resize', update);
      }catch(e){ console.warn(e); }
    }
    // 对所有卡片工具区初始化“更多”菜单
    grid.querySelectorAll('.radish-card-tools').forEach(setupToolsMore);

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

    // Hidden sync（同时作用于“应用列表”和“收藏”两个容器）
    function syncHidden(){
      var hidden = getHidden();
      try{
        var containers = [grid];
        var favRow = document.getElementById('favRow'); if(favRow) containers.push(favRow);
        containers.forEach(function(container){
          if(!container) return;
          Array.from(container.querySelectorAll(':scope > .col')).forEach(function(col){
            var id = col.getAttribute('data-client-id')||'';
            col.classList.toggle('d-none', hidden.indexOf(id) >= 0);
          });
        });
      }catch{}
    }
    syncHidden();

    // 收藏区重排：将已收藏卡片移动到“收藏”区，其余保留在“应用列表”区
    function reflowByFavorites(){
      try{
        var favRow = document.getElementById('favRow');
        var favSection = document.getElementById('favSection');
        if(!favRow){ return; }
        var favs = getFavs();
        var order = getOrder(); var omap = {}; order.forEach(function(id,i){ omap[id]=i; });
        // 收集两个容器中的卡片
        var items = [];
        [grid, favRow].forEach(function(c){ if(c){ items = items.concat(Array.from(c.querySelectorAll(':scope > .col'))); } });
        // 分组
        var favItems = []; var otherItems = [];
        items.forEach(function(col){ var id=col.getAttribute('data-client-id')||''; if(favs.indexOf(id)>=0) favItems.push(col); else otherItems.push(col); });
        // 根据自定义顺序排序（无顺序的放后，按文本）
        function sortItems(arr){ arr.sort(function(a,b){ var ia=omap[a.getAttribute('data-client-id')||'']; var ib=omap[b.getAttribute('data-client-id')||'']; ia=(typeof ia==='number')?ia:9999; ib=(typeof ib==='number')?ib:9999; if(ia!==ib) return ia-ib; return (a.textContent||'').localeCompare((b.textContent||'')); }); }
        sortItems(favItems); sortItems(otherItems);
        // 移动节点到对应容器
        favItems.forEach(function(el){ favRow.appendChild(el); });
        otherItems.forEach(function(el){ grid.appendChild(el); });
        // 显隐收藏区
        if(favSection){ favSection.classList.toggle('d-none', favItems.length === 0); }
        // 应用隐藏规则
        syncHidden();
      }catch(e){ console.warn(e); }
    }

    // Favorites init + toggle
    var favs = getFavs();
    function toggleFav(id){ var pos = favs.indexOf(id); if(pos>=0) favs.splice(pos,1); else favs.push(id); setFavs(favs); return favs; }
    grid.querySelectorAll('.fav-toggle').forEach(function(el){
      var id = el.getAttribute('data-client-id')||'';
      if(favs.indexOf(id) >= 0){ el.classList.add('is-fav'); el.textContent = '★'; } else { el.textContent = '☆'; }
      el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation();
        var now = toggleFav(id);
        if(now.indexOf(id) >= 0){ el.classList.add('is-fav'); el.textContent = '★'; } else { el.classList.remove('is-fav'); el.textContent = '☆'; }
        try{ reflowByFavorites(); }catch{}
        // 可能影响“仅显示收藏”的最近访问展示
        try{ renderRecent(); }catch{}
      });
    });
    // 初始重排一次
    reflowByFavorites();

    // Copy link
    grid.querySelectorAll('.copy-link').forEach(function(el){ el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); var href = el.getAttribute('data-href')||''; if(navigator.clipboard){ navigator.clipboard.writeText(href).then(function(){ if(window.abp&&abp.notify){ abp.notify.info(L('Notify:Copied')); } else { console.info(L('Notify:Copied')+': '+href); } }).catch(function(){ alert(L('Notify:CopyFailed')); }); } else { var t=document.createElement('textarea'); t.value=href; document.body.appendChild(t); t.select(); try{ document.execCommand('copy'); if(window.abp&&abp.notify){ abp.notify.info(L('Notify:Copied')); } } finally { document.body.removeChild(t); } } }); });

    // Health check
    grid.querySelectorAll('.health-check').forEach(function(el){ el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); var origin = el.getAttribute('data-health-origin')||''; if(!origin){ if(window.abp&&abp.notify) abp.notify.warn(L('Notify:HealthNotConfigured')); return; } var url = origin.replace(/\/$/,'') + '/health-status'; fetch(url,{method:'GET'}).then(function(r){ if(!r.ok) throw new Error('HTTP '+r.status); return r.json(); }).then(function(data){ var status=(data&&data.status)||'Unknown'; var msg=L('Notify:HealthStatus', status); if(window.abp&&abp.notify){ if((status||'').toLowerCase()==='healthy') abp.notify.success(msg); else abp.notify.warn(msg); } else { alert(msg); } }).catch(function(){ if(window.abp&&abp.notify) abp.notify.error(L('Notify:HealthCheckFailed')); else alert(L('Notify:HealthCheckFailed')); }); }); });

    // Recent
    function touchRecent(item){ var list=getRecent(); list=list.filter(function(x){return x&&x.id!==item.id;}); list.unshift(item); if(list.length>2) list=list.slice(0,2); setRecent(list); }
    grid.querySelectorAll('.radish-app-card').forEach(function(a){ a.addEventListener('click', function(){ var parent=a.closest('.col'); var id=parent?(parent.getAttribute('data-client-id')||''):''; var title=a.getAttribute('data-title')||(a.querySelector('.h5')?a.querySelector('.h5').textContent.trim():id); var href=a.getAttribute('data-href')||a.getAttribute('href')||''; touchRecent({id:id, href:href, title:title}); }); });
    function renderRecent(){
      var hidden=getHidden();
      var recent=getRecent().filter(function(x){ return x && hidden.indexOf(x.id) < 0; });
      var onlyFavs = (localStorage.getItem(KEY_RECENT_ONLYFAVS) === 'true');
      if(onlyFavs){ var favs=getFavs(); recent = recent.filter(function(x){ return favs.indexOf(x.id) >= 0; }); }
      // 仅保留最新的两条
      recent = recent.slice(0, 2);
      var section=document.getElementById('recentSection'); var row=document.getElementById('recentRow'); if(!row||!section) return;
      if(!recent.length){ section.classList.add('d-none'); return; }
      section.classList.remove('d-none'); row.innerHTML='';
      recent.forEach(function(r){ var col=document.createElement('div'); col.className='col'; col.innerHTML='\n<a href="'+(r.href||'#')+'" target="_blank" class="radish-app-card d-block h-100 text-reset">\n  <div class="card h-100 border-0 shadow-sm radish-card">\n    <div class="card-body d-flex align-items-start gap-3">\n      <div class="flex-grow-1">\n        <div class="h6 mb-1">'+(r.title||r.id||'App')+'</div>\n        <div class="text-muted small text-truncate">'+(r.href||'')+'</div>\n      </div>\n    </div>\n  </div>\n</a>'; row.appendChild(col); });
    }
    renderRecent();
    // Recent section controls
    try{
      var onlyFavsToggle = document.getElementById('recentOnlyFavsToggle');
      if(onlyFavsToggle){ onlyFavsToggle.checked = (localStorage.getItem(KEY_RECENT_ONLYFAVS) === 'true');
        onlyFavsToggle.addEventListener('change', function(){ localStorage.setItem(KEY_RECENT_ONLYFAVS, onlyFavsToggle.checked ? 'true':'false'); renderRecent(); }); }
      var clearBtn = document.getElementById('recentClearBtn');
      if(clearBtn){ clearBtn.addEventListener('click', function(){ setRecent([]); renderRecent(); if(window.abp&&abp.notify) abp.notify.info(L('Actions:Clear')); }); }
    }catch{}

    // Hide toggle & manage
    grid.querySelectorAll('.hide-toggle').forEach(function(el){ el.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); var id=el.getAttribute('data-client-id')||''; var arr=getHidden(); if(arr.indexOf(id)<0) arr.push(id); setHidden(arr); syncHidden(); renderRecent(); if(window.abp&&abp.notify) abp.notify.info(L('Notify:HiddenDone')); }); });

    function openHiddenManage(){ try{ var list=document.getElementById('hiddenList'); if(!list) return; var arr=getHidden(); var meta=getMeta(); list.innerHTML=''; if(!arr.length){ list.innerHTML='<div class="text-muted">'+L('Hidden:Empty')+'</div>'; } arr.forEach(function(id){ var m=meta[id]||{title:id, href:''}; var item=document.createElement('div'); item.className='list-group-item d-flex justify-content-between align-items-center'; item.innerHTML='<div><div class="fw-semibold">'+(m.title||id)+'</div><div class="text-muted small">'+(m.href||'')+'</div></div><button type="button" class="btn btn-sm btn-outline-primary unhide" data-id="'+id+'">'+L('Actions:Show')+'</button>'; list.appendChild(item); }); list.querySelectorAll('.unhide').forEach(function(btn){ btn.addEventListener('click', function(){ var id=btn.getAttribute('data-id')||''; var a=getHidden().filter(function(x){return x!==id;}); setHidden(a); syncHidden(); renderRecent(); btn.closest('.list-group-item').remove(); }); }); var modalEl=document.getElementById('hiddenManageModal'); if(!modalEl) return; var mdl=new bootstrap.Modal(modalEl); mdl.show(); }catch(e){ console.warn(e); } }
    var hBtn=document.getElementById('hiddenManageBtn'); if(hBtn) hBtn.addEventListener('click', openHiddenManage);
    var hBtnM=document.getElementById('hiddenManageBtnMobile'); if(hBtnM) hBtnM.addEventListener('click', openHiddenManage);

    // Edit mode (drag & drop)
    var editBtn = document.getElementById('editToggle');
    var editBtnM = document.getElementById('editToggleMobile');
    function readIds(container){ return Array.from(container.querySelectorAll(':scope > .col')).map(function(x){ return x.getAttribute('data-client-id')||'';}); }
    function setEditMode(on){ document.body.classList.toggle('edit-mode', !!on); grid.querySelectorAll(':scope > .col').forEach(function(col){ col.setAttribute('draggable', on? 'true':'false'); }); if(editBtn) editBtn.textContent = on? L('Actions:Done'):L('Actions:Edit'); }
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
