/*
  Simple force-directed renderer for the viewer.
  Provides basic physics, dragging and click events without external libraries so the app works offline.
*/
(function(window){
  function rand(min,max){ return min + Math.random()*(max-min); }

  function Network(container, data, options){
    this.container = container;
    this.rawNodes = (data.nodes || []).map(n=>Object.assign({}, n));
    this.rawEdges = (data.edges || []).map(e=>Object.assign({}, e));
    this.handlers = {};
    this.width = container.clientWidth || 800;
    this.height = container.clientHeight || 600;
    this.svg = document.createElementNS('http://www.w3.org/2000/svg','svg');
    this.svg.setAttribute('width','100%');
    this.svg.setAttribute('height','100%');
    this.svg.style.touchAction = 'none';
    while (container.firstChild) container.removeChild(container.firstChild);
    container.appendChild(this.svg);

    // initialize nodes with positions and velocities
    this.nodes = this.rawNodes.map((n,i)=>({
      id: n.id,
      label: n.label,
      rep: n.rep,
      x: rand(this.width*0.2,this.width*0.8),
      y: rand(this.height*0.2,this.height*0.8),
      vx: 0, vy: 0,
      mass: 1,
      fx:0, fy:0
    }));
    this.edges = this.rawEdges.map(e=>({from: e.from, to: e.to}));

    this.nodeMap = {};
    this.nodes.forEach(n=>this.nodeMap[n.id]=n);

    this.g = document.createElementNS('http://www.w3.org/2000/svg','g');
    this.svg.appendChild(this.g);

    this.edgeLayer = document.createElementNS('http://www.w3.org/2000/svg','g');
    this.nodeLayer = document.createElementNS('http://www.w3.org/2000/svg','g');
    this.g.appendChild(this.edgeLayer);
    this.g.appendChild(this.nodeLayer);

    this.nodeElements = {};

    const self = this;
    // build elements
    for(let e of this.edges){
      const line = document.createElementNS('http://www.w3.org/2000/svg','line');
      line.setAttribute('stroke','#aaa'); line.setAttribute('stroke-width','1.2');
      this.edgeLayer.appendChild(line);
      e._el = line;
    }

    for(let n of this.nodes){
      const g = document.createElementNS('http://www.w3.org/2000/svg','g');
      const rect = document.createElementNS('http://www.w3.org/2000/svg','rect');
      rect.setAttribute('x',-50); rect.setAttribute('y',-18); rect.setAttribute('width',100); rect.setAttribute('height',36);
      rect.setAttribute('rx',6); rect.setAttribute('fill','#fff'); rect.setAttribute('stroke','#333');
      const text = document.createElementNS('http://www.w3.org/2000/svg','text');
      text.setAttribute('x',0); text.setAttribute('y',5); text.setAttribute('text-anchor','middle'); text.setAttribute('font-size','11');
      text.textContent = n.label;
      g.appendChild(rect); g.appendChild(text);
      this.nodeLayer.appendChild(g);
      n._el = g;
      // events
      (function(id){
        let dragging = false;
        let offset = {x:0,y:0};
        g.addEventListener('pointerdown', function(ev){
          g.setPointerCapture(ev.pointerId);
          dragging = true;
          offset.x = n.x - ev.clientX;
          offset.y = n.y - ev.clientY;
        });
        window.addEventListener('pointermove', function(ev){
          if (!dragging) return;
          n.x = ev.clientX + offset.x;
          n.y = ev.clientY + offset.y;
          n.vx = 0; n.vy = 0;
        });
        window.addEventListener('pointerup', function(ev){
          if (dragging){ dragging = false; try{ g.releasePointerCapture(ev.pointerId);}catch{} }
        });
        g.addEventListener('click', function(ev){ ev.stopPropagation(); if (g._clickHandler) g._clickHandler({nodes:[id]}); });
      })(n.id);
    }

    // simulation parameters
    this.alpha = 0.1;
    this.repulsion = 4000; // strength
    this.springK = 0.02;
    this.damping = 0.85;

    this.running = true;
    const step = ()=>{
      if (!self.running) return;
      self.simulate();
      self.render();
      requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  Network.prototype.simulate = function(){
    // reset forces
    for(let n of this.nodes){ n.fx = 0; n.fy = 0; }
    // repulsion
    for(let i=0;i<this.nodes.length;i++){
      for(let j=i+1;j<this.nodes.length;j++){
        const a=this.nodes[i], b=this.nodes[j];
        let dx = a.x - b.x, dy = a.y - b.y;
        let dist2 = dx*dx + dy*dy + 0.01;
        let dist = Math.sqrt(dist2);
        let force = this.repulsion / dist2;
        let fx = (dx/dist) * force, fy = (dy/dist) * force;
        a.fx += fx; a.fy += fy; b.fx -= fx; b.fy -= fy;
      }
    }
    // springs
    for(let e of this.edges){
      const a = this.nodeMap[e.from];
      const b = this.nodeMap[e.to];
      if (!a || !b) continue;
      let dx = b.x - a.x, dy = b.y - a.y;
      let dist = Math.sqrt(dx*dx + dy*dy) || 1;
      let desired = 120; // rest length
      let k = this.springK;
      let fs = k * (dist - desired);
      let fx = (dx/dist) * fs, fy = (dy/dist) * fs;
      a.fx += fx; a.fy += fy; b.fx -= fx; b.fy -= fy;
    }
    // integrate
    for(let n of this.nodes){
      n.vx = (n.vx + n.fx * this.alpha) * this.damping;
      n.vy = (n.vy + n.fy * this.alpha) * this.damping;
      n.x += n.vx; n.y += n.vy;
      // bounds
      n.x = Math.max(20, Math.min(this.container.clientWidth-20, n.x));
      n.y = Math.max(20, Math.min(this.container.clientHeight-20, n.y));
    }
  };

  // Pan & zoom support: uses transform on group 'g'
  Network.prototype.setTransform = function(tx, ty, scale){
    this._tx = tx; this._ty = ty; this._scale = scale;
    this.g.setAttribute('transform', 'translate(' + tx + ',' + ty + ') scale(' + scale + ')');
  };

  Network.prototype.enableInteractions = function(){
    const self = this;
    if (this._interactionsEnabled) return;
    this._interactionsEnabled = true;
    // initial transform
    this._tx = 0; this._ty = 0; this._scale = 1;
    // wheel zoom
    this.container.addEventListener('wheel', function(ev){
      ev.preventDefault();
      const rect = self.container.getBoundingClientRect();
      const mx = ev.clientX - rect.left; const my = ev.clientY - rect.top;
      const delta = -ev.deltaY * 0.0015;
      const newScale = Math.max(0.2, Math.min(3, self._scale * (1 + delta)));
      // compute new translate to zoom around mouse
      const sx = mx - self._tx; const sy = my - self._ty;
      self._tx = mx - sx * (newScale / self._scale);
      self._ty = my - sy * (newScale / self._scale);
      self._scale = newScale;
      self.setTransform(self._tx, self._ty, self._scale);
    }, { passive: false });

    // pan with middle mouse or right mouse
    let panning = false; let start = {x:0,y:0};
    this.container.addEventListener('pointerdown', function(ev){
      if (ev.button === 1 || ev.button === 2){ panning = true; start.x = ev.clientX; start.y = ev.clientY; self.container.setPointerCapture(ev.pointerId); }
    });
    window.addEventListener('pointermove', function(ev){ if (!panning) return; const dx = ev.clientX - start.x; const dy = ev.clientY - start.y; start.x = ev.clientX; start.y = ev.clientY; self._tx += dx; self._ty += dy; self.setTransform(self._tx, self._ty, self._scale); });
    window.addEventListener('pointerup', function(ev){ if (panning){ panning = false; try{ self.container.releasePointerCapture(ev.pointerId);}catch{} } });
  };

  Network.prototype.render = function(){
    // update edges
    for(let e of this.edges){
      const a = this.nodeMap[e.from];
      const b = this.nodeMap[e.to];
      if (!a || !b || !e._el) continue;
      e._el.setAttribute('x1', a.x); e._el.setAttribute('y1', a.y);
      e._el.setAttribute('x2', b.x); e._el.setAttribute('y2', b.y);
    }
    // update nodes
    for(let n of this.nodes){
      if (!n._el) continue;
      n._el.setAttribute('transform','translate('+n.x+','+n.y+')');
      // update text in case label changed
      const txt = n._el.querySelector('text');
      if (txt) txt.textContent = n.label;
      // highlight selected
      if (this._selectedId === n.id) {
        const rect = n._el.querySelector('rect');
        if (rect) rect.setAttribute('stroke', '#ff8800');
      } else {
        const rect = n._el.querySelector('rect');
        if (rect) rect.setAttribute('stroke', '#333');
      }
    }
  };

  Network.prototype.on = function(evt, handler){
    if (evt === 'click'){
      for(let n of this.nodes){ if (n._el) n._el._clickHandler = handler; }
    }
  };

  Network.prototype.destroy = function(){ this.running = false; if (this.svg && this.svg.parentNode) this.svg.parentNode.removeChild(this.svg); this.svg = null; };

  window.vis = { Network: Network };
})(window);
