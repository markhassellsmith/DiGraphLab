(function(window){
  // Minimal local vis-like network renderer for demo purposes
  function Network(container, data, options){
    this.container = container;
    this.nodes = data.nodes || [];
    this.edges = data.edges || [];
    this.handlers = {};
    // create svg
    this.svg = document.createElementNS('http://www.w3.org/2000/svg','svg');
    this.svg.setAttribute('width','100%');
    this.svg.setAttribute('height','100%');
    while (container.firstChild) container.removeChild(container.firstChild);
    container.appendChild(this.svg);
    // render simple circular layout
    this.render = function(){
      while (this.svg.firstChild) this.svg.removeChild(this.svg.firstChild);
      var w = container.clientWidth, h = container.clientHeight;
      var cx = w/2, cy = h/2, r = Math.min(w,h)/3;
      var n = this.nodes.length;
      this.nodeElements = {};
      for(var i=0;i<n;i++){
        var node = this.nodes[i];
        var angle = (i / n) * Math.PI * 2;
        var x = cx + r * Math.cos(angle);
        var y = cy + r * Math.sin(angle);
        // draw edge lines later
        node._x = x; node._y = y;
      }
      // draw edges
      for(var e of this.edges){
        var from = this.nodes.find(x=>x.id==e.from);
        var to = this.nodes.find(x=>x.id==e.to);
        if (!from || !to) continue;
        var line = document.createElementNS('http://www.w3.org/2000/svg','line');
        line.setAttribute('x1',from._x); line.setAttribute('y1',from._y);
        line.setAttribute('x2',to._x); line.setAttribute('y2',to._y);
        line.setAttribute('stroke','#999'); line.setAttribute('stroke-width','1.5');
        this.svg.appendChild(line);
      }
      // draw nodes as groups
      for(var i=0;i<n;i++){
        var node = this.nodes[i];
        var g = document.createElementNS('http://www.w3.org/2000/svg','g');
        g.setAttribute('transform','translate('+node._x+','+node._y+')');
        var rect = document.createElementNS('http://www.w3.org/2000/svg','rect');
        rect.setAttribute('x',-40); rect.setAttribute('y',-18); rect.setAttribute('width',80); rect.setAttribute('height',36);
        rect.setAttribute('rx',6); rect.setAttribute('fill','#fff'); rect.setAttribute('stroke','#333');
        var text = document.createElementNS('http://www.w3.org/2000/svg','text');
        text.setAttribute('x',0); text.setAttribute('y',5); text.setAttribute('text-anchor','middle');
        text.setAttribute('font-size','10'); text.textContent = node.label;
        g.appendChild(rect); g.appendChild(text);
        (function(id, nodeObj){
          g.addEventListener('click', function(ev){
            if (this._clickHandler) this._clickHandler({nodes:[id]});
            ev.stopPropagation();
          });
        })(node.id, node);
        this.svg.appendChild(g);
        this.nodeElements[node.id] = g;
      }
    };
    var self = this;
    // initial render
    setTimeout(function(){ self.render(); }, 10);
  }
  Network.prototype.on = function(evt, handler){
    if (evt === 'click'){
      // store handler and attach to node groups
      this.handlers.click = handler;
      for(var id in this.nodeElements){
        var el = this.nodeElements[id];
        el._clickHandler = handler;
      }
    }
  };
  Network.prototype.destroy = function(){
    if (this.svg && this.svg.parentNode) this.svg.parentNode.removeChild(this.svg);
    this.svg = null;
  };
  window.vis = { Network: Network };
})(window);
