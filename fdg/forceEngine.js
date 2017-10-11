var fdg_defaultTexture;

function fdg_init(){
	let m=new PIXI.Graphics();
	m.lineStyle(2,0xffffff);
	m.beginFill(0x333333);
	m.drawCircle(0,0,10);

	fdg_defaultTexture=app.renderer.generateTexture(m);
}

function fdg_ForceContainer(par){
	/****************************************
		Force Container - Create an instance of this to hold all of your force elements
		Functions to call:
			addNewNode - Creates a new node
			addNode - Adds a node created manually
			removeNode - removes a node by direct reference
			removeNodeByIndex - remove a node given its index in the nodes array
			addLink - creates a link between two nodes
			removeLink - removes a link between two nodes
			drawLinks - *ticker* draws all the links
			applyForces - *ticker* applies physics stuff and calls Node internal functions (including graphical -- bad practice)
			getClosestObject - finds the closest object to any given point
	****************************************/
	par = par || {};
	this.canvas=par.canvas || new PIXI.Sprite(); //where Nodes will be added
	this.bgCanvas=par.bgCanvas || new PIXI.Graphics(); //where Links will be drawn
	this.borders=par.borders || {left:0,top:0,right:1000,bot:1000}; //bounds set for where nodes will bounce
	this.separator=new PIXI.Sprite; //all newly added Nodes will be added at this child index
	if (this.canvas!=null){
		this.canvas.addChild(this.separator);
	}

	this.nodes=new Array(); //An array of all nodes that currently exist
	this.links=new Array(); //An array of all links that currently exist
	this.pulls=new Array(); //An array of special pulls that exist (ie. Mouse Cursor)

	this.MAX_GRAB=par.MAX_GRAB || 30; //how far away by default the 'getClosestObject' function checks

	//TARGET = Mouse Location, or anywhere else you want to force a Node to move to
	this.TARGET_RATIO=0.008; //How quickly an object will follow the target
	this.TARGET_MIN=100; //The minimum distance away before a node will follow the target NOTE: pixel*pixel distance 

	this.bounce=0.1; //The bounciness of walls

	this.addPull=function(_pull){
		this.pulls.push(_pull);
	}

	this.removePull=function(_pull){
		for (var i=this.pulls.length-1;i>=0;i-=1){
			if (this.pulls[i]===_pull){
				this.pulls.splice(i,1);
				return;
			}
		}
	}

	this.addNewNode=function(par){ //Call to create a new default node with given parameters and add it to the container
		//OPTIONAL PARAMETERS: canvas, where to add the Sprite
		//						see 'fdg_Node' for more options
		par=par || {};
		par.canvas=par.canvas || this.canvas;
		let m=new fdg_Node(par);
		return this.addNode(m);
	}

	this.addNode=function(_node){ //Called from 'addNewNode' after a node has been created.  Call manually if you want to create a custom node
		this.nodes.push(_node);
		let _sprite=_node.makeSprite(_node.texture);
		this.canvas.addChildAt(_sprite,this.canvas.getChildIndex(this.separator));
		return _node;
	}

	this.removeNode=function(_node){ //Removes a node instance
		for (var i=0;i<this.nodes.length;i+=1){
			if (_node===this.nodes[i]){
				this.removeNodeByIndex(i);
				return;
			}
		}
	}

	this.removeNodeByIndex=function(i){ //Called from 'removeNode', or call directly if you know the index of the node you want to remove
		if (this.nodes[i].onRemove!=null) this.nodes[i].onRemove();
		for (var j=0;j<this.links.length;j+=1){
			if (this.links[j].origin===this.nodes[i] || this.links[j].target===this.nodes[i]){
				this.links.splice(j,1);
				j-=1;
			}
		}
		while(this.nodes[i].outlets.length>0){
			for (j=0;j<this.nodes[i].outlets[0].node.outlets.length;j+=1){
				if (this.nodes[i].outlets[0].node.outlets[j].node===this.nodes[i]){
					this.nodes[i].outlets[0].node.outlets.splice(j,1);
					break;
				}
			}
			this.nodes[i].outlets.shift();
		}
		if (this.nodes[i].sprite!=null)
			this.nodes[i].sprite.destroy();
		this.nodes.splice(i,1);
	}

	this.removeLink=function(_node,_other){ //Removes a link between two supplied nodes
		for (var i=0;i<this.links.length;i+=1){
			if ((this.links[i].origin==_node && this.links[i].target==_other) || 
				(this.links[i].origin==_other && this.links[i].target==_node)){
				this.links.splice(i,1);
				return;
			}
		}
	}
	this.addNewLink=function(par){
		this.addLink(new fdg_Link(par));
	}

	this.addLink=function(_link){ //adds a link between two supplied nodes
		//REQUIRED PARAMETERS: origin, target
		//OPTIONAL PARAMETERS: See 'fdg_Link'
		this.links.push(_link);
		
		return _link;
	}

	this.drawLinks=function(){
		//Call this in your main ticker to draw the links on bgCanvas
		var _links=this.links;
		this.bgCanvas.clear();
		for (var i=this.links.length-1;i>=0;i-=1){
			this.bgCanvas.lineStyle(this.links[i].lineStyle,this.links[i].color*this.links[i].intensity);
			this.bgCanvas.moveTo(this.links[i].origin.x,this.links[i].origin.y);
			this.bgCanvas.lineTo(this.links[i].target.x,this.links[i].target.y);
		}
	}
	
	this.applyForces=function(_andSprite){
		//Call this in your main ticker to apply (1) 

		//LINK FORCE -- Actives all links
		for (var i=0;i<this.links.length;i+=1){
			let _link=this.links[i];
			if (_link.onTick!=null) _link.onTick();
			let _node=_link.origin;
			let _other=_link.target;

			let _dX=(_node.x-_other.x);
			let _dY=(_node.y-_other.y);
			let _dist2=_dX*_dX+_dY*_dY;
			let _dist=Math.sqrt(_dist2);
			if (_dist<_link.length) continue;
			//** PULL FORMULA **

			let _mult=-_link.elasticity*(_dist-_link.length)/_dist;

			_node.vX+=_mult*_dX*_node.invMass;
			_node.vY+=_mult*_dY*_node.invMass;
			_other.vX-=_mult*_dX*_other.invMass;
			_other.vY-=_mult*_dY*_other.invMass;
		}

		//Special Pulls (usually means Mouse Cursor(s))
		for (var i=this.pulls.length-1;i>=0;i-=1){
			let _pull=this.pulls[i];

			_pull.drag.vX*=0.7;
			_pull.drag.vY*=0.7;
			let _dX=_pull.drag.x-_pull.x;
			let _dY=_pull.drag.y-_pull.y;
			let _dist=_dX*_dX+_dY*_dY;

			if (_dist>this.TARGET_MIN){
				let _mult=-this.TARGET_RATIO;
				_pull.drag.vX+=_mult*_dX;
				_pull.drag.vY+=_mult*_dY;
			}
		}
		
		//loops through nodes
		i=this.nodes.length-1;
		if (i<0) return;
		do{
			//REPULSE FORCE -- Between all objects
			let _node=this.nodes[i];
			let j=this.nodes.length-1;
			while (j>i){
				let _other=this.nodes[j];
				let _dX=(_node.x-_other.x);
				let _dY=(_node.y-_other.y);
				if (_dX>0) _dX=Math.max(2,_dX);
				else _dX=Math.min(-2,_dX);
				if (_dY>0) _dY=Math.max(2,_dY);
				else _dY=Math.min(-2,_dY);

				let _dist2=_dX*_dX+_dY*_dY;
				let _mult=_node.force*_other.force/_dist2/Math.sqrt(_dist2);
				
				_node.vX+=_mult*_dX*_node.invMass;
				_node.vY+=_mult*_dY*_node.invMass;
				_other.vX-=_mult*_dX*_other.invMass;
				_other.vY-=_mult*_dY*_other.invMass;
				j-=1;
			};

			//BORDER -- Bounce
			if (_node.x<this.borders.left) _node.vX+=this.bounce*(-_node.x+this.borders.left);
			if (_node.y<this.borders.top) _node.vY+=this.bounce*(-_node.y+this.borders.top);
			if (_node.x>this.borders.right) _node.vX+=this.bounce*(-_node.x+this.borders.right);
			if (_node.y>this.borders.bot) _node.vY+=this.bounce*(-_node.y+this.borders.bot);

			_node.onTickMain(); //Calls the node's main ticker function
			if (_andSprite) _node.updateSprite();
		}while(i--);
		
	}

	this.getClosestObject=function(par){
		//Gets the closest object to a suplied location
		//Required Parameters: x,y locations
		//Optional Parameters: distance: Maximum distance to check, 
		//					   maxLinks: if a node has maxed out the links property
		//					   filter: a node to not include in the check (ie. checking closest node to another node)
		var m=null;
		var _distance=par.distance*par.distance || this.MAX_GRAB*this.MAX_GRAB;
		var _distance2=0;
		
		for (var i=0;i<this.nodes.length;i+=1){
			if (par.maxLinks && this.nodes[i].outlets.length>=this.nodes[i].maxLinks) continue;
			if (par.filter!=null && par.filter===this.nodes[i]) continue;
			if (par.hasTag!=null && par.hasTag!==this.nodes[i].tag) continue;
			if (par.notHasTag!=null && par.notHasTag===this.nodes[i].tag) continue;
			let _x2=this.nodes[i].x-par.x;
			let _y2=this.nodes[i].y-par.y;
			_distance2=_x2*_x2+_y2*_y2;
			if (_distance2<_distance){
				_distance=_distance2;
				m=this.nodes[i];
			}
		}
		return m;
	}
}

function fdg_Node(par){
	/********************************
		Node Object: This is what is added as a floating object.  Anything you want to add to the forces must subclass this class
		Recommended Parameters: x, y
		Functions you may need to call manually: NONE
		**Check 'makeSprite' function and update the 'textures' reference
	********************************/
	par=par || {};
	//visual stuff
	this.radius=par.radius || 10; //The radius, used in certain calculations
	this.color=par.color || 0x001524; //Tints the object by this color
	this.shape=par.shape || 0; //0-Circle, 1-Square, 2-Triangle, 3-Thin Rect, 4-Fat Rect, 5-Pentagon, 6-Hexagon
								//References the 'textures' array

	//physics stuff
	this.x=par.x || 0; //x location
	this.y=par.y || 0; //y location
	this.vX=par.vX || 0; //x velocity
	this.vY=par.vY || 0; //y velocity
	this.force=(par.force!=null ? par.force : 1); //Repulse Force of this object
	this.mass=par.mass || this.radius/10; //An objects simulated mass (slows down all interactions)
	this.invMass=1/this.mass; //actually used for calculations
	this.rotation=0; //rotation
	this.rotationSpeed= (par.rotationSpeed!=null ? par.rotationSpeed : 0.005); //how fast it rotates by default
	this.x-=this.radius;
	this.y-=this.radius;
	//this.active=(par.active!=null ? par.active : true); //if not active, adds 'friction'
	this.fixed=par.fixed || false; //cannot be moved
	this.target=par.target || null; //follows this target; must have {x,y} properties
	this.damp=par.damp || 0.98; //General friction
	this.minV=par.minV || 0.000001; //The slowest an object can move before rounding to 0
	
	//fdg stuff
	this.maxLinks= (par.maxLinks!=null ? par.maxLinks : 6); //Only used in the 'getClosestObject' filter
	this.outlets=new Array; //Stores a reference to anything linked: {node,link}

	//functions
	this.onAdd=par.onAdd || null; //function to call when this object is first added
	this.onTick=par.onTick || null; //function to add every tick (based on applyForces)
	this.onRemove=par.onRemove || null; //function to add when this object is removed
	
	this.rotate=function(){ //call this to rotate the object, called automatically from moveBody
		this.rotation+=this.rotationSpeed;
		if (this.rotation>Math.PI) this.rotation-=Math.PI*2;
		if (this.rotation<-Math.PI) this.rotation+=Math.PI*2;
	}

	this.onTickMain=function(){ //run every tick from 'applyForces'
		this.moveBody();
		if (this.onTick!=null) this.onTick();
	}

	this.moveBody=function(){ //finishes moving this object
		this.rotate();

		if (this.fixed) return;
		
		this.vX*=this.damp;
		this.vY*=this.damp;
		
		if (Math.abs(this.vX)>this.minV || Math.abs(this.vY)>this.minV){
			this.x+=this.vX;
			this.y+=this.vY;
		}else{
			this.vX=this.vY=0;
		}
	}

	this.makeSprite=function(_texture=null){ //Creates the sprite associated with this object
		this.sprite=new PIXI.Sprite(_texture || fdg_defaultTexture);
			//NOTE:may want to connect 'textures' to 'type' instead of shape and radius
		this.sprite.pivot.x=this.sprite.width*0.5;
		this.sprite.pivot.y=this.sprite.height*0.5;
		return this.sprite;
	}

	this.updateSprite=function(){ //updates the sprite from this node's properties.  Called every tick through 'onTickMain'
		let _sprite=this.sprite;
		if (_sprite==null) return;
		
		_sprite.rotation=this.rotation;
		this.sprite.x=this.x;
		this.sprite.y=this.y;
	}


	if (this.onAdd!=null) this.onAdd();
}



function fdg_Link(par){
	/********************************
		Link: Binds two nodes together
		Recommended Parameters: NONE
		Functions you may need to call manually: 
			flip - exchanges 'origin' and 'target' positions
			lineCrosses - check if this line crosses another line
	********************************/
	this.origin=par.origin;
	this.target=par.target;
	this.elasticity=par.elasticity || 0.004;
	this.length=par.length || 40;
	this.color=par.color || 0x222222;
	this.lineStyle=par.lineStyle || 2;
	this.type=0; //0 is Link
	this.onTick=par.onTick || null;
	this.intensity=par.intensity || 2;
	if (!par.noOutlets){
		par.target.outlets.push({link:this,node:par.origin});
		par.origin.outlets.push({link:this,node:par.target});
	}
	this.flip=function(){
		let _temp=this.target;
		this.target=this.origin;
		this.origin=_temp;
	}

	this.lineCrosses=function(_x1,_y1,_x2,_y2){
		let A1=_y2-_y1;
		let B1=_x1-_x2;
		let C1=A1*_x1+B1*_y1;

		let A2=this.target.y-this.origin.y;
		let B2=this.origin.x-this.target.x;
		let C2=A2*this.origin.x+B2*this.origin.y;

		let det=A1*B2 - A2*B1;

		if (det==0) return false;

		let _x=(B2*C1 - B1*C2)/det;
		let _y=(A1*C2 - A2*C1)/det;

		if (Math.min(_x1,_x2)<=_x && Math.min(_y1,_y2)<=_y &&
			Math.max(_x1,_x2)>=_x && Math.max(_y1,_y2)>=_y &&
			Math.min(this.origin.x,this.target.x)<=_x && 
			Math.min(this.origin.y,this.target.y)<=_y &&
			Math.max(this.origin.x,this.target.x)>=_x &&
			Math.max(this.origin.y,thistarget.y)>=_y){
				return true;
		}
		return false;
	}
}
