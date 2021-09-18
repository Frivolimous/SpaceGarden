const textures=[]; //holds all the textures used for making Nodes
const crawlerTextures=[]; //holds all the textures used for making Crawlers

const NodeTags={
	NORMAL:0,
	FRUIT:1
}

function nodes_init(){ //call this function to build the textures array
	for (var i=5;i<=30;i+=5){
		let _graphic=new PIXI.Graphics();
		_graphic.lineStyle(2,0xffffff);
		_graphic.beginFill(0x333333);
		_graphic.drawCircle(0,0,i);
		textures.push(app.renderer.generateTexture(_graphic));
	}
	let _polygons=[
				[{x:-1,y:-1},{x:-1,y:1},{x:1,y:1},{x:1,y:-1}],
				[{x:-1,y:0.75},{x:0,y:-1},{x:1,y:0.75}],
				[{x:-1,y:-0.3},{x:-1,y:0.3},{x:1,y:0.3},{x:1,y:-0.3}],
				[{x:-1,y:-0.7},{x:-1,y:0.7},{x:1,y:0.7},{x:1,y:-0.7}],
				[{x:1,y:0},{x:(Math.sqrt(5)-1)/4,y:-Math.sqrt(10+2*Math.sqrt(5))/4},{x:-(Math.sqrt(5)+1)/4,y:-Math.sqrt(10-2*Math.sqrt(5))/4},{x:-(Math.sqrt(5)+1)/4,y:Math.sqrt(10-2*Math.sqrt(5))/4},{x:(Math.sqrt(5)-1)/4,y:Math.sqrt(10+2*Math.sqrt(5))/4}],
				[{x:1,y:0},{x:1/2,y:Math.sqrt(3)/2},{x:-1/2,y:Math.sqrt(3)/2},{x:-1,y:0},{x:-1/2,y:-Math.sqrt(3)/2},{x:1/2,y:-Math.sqrt(3)/2}]
				];
				//0-Circle, 1-Square, 2-Triangle, 3-Thin Rect, 4-Fat Rect, 5-Pentagon, 6-Hexagon
	let transformPolygon=function(_poly,_scale){
		let m=[];
		for (var i=0;i<_poly.length;i+=1){
			m.push(new PIXI.Point(_poly[i].x*_scale,_poly[i].y*_scale));
		}
		m.push(new PIXI.Point(_poly[0].x*_scale,_poly[0].y*_scale));
		return m;
	}
	for (var j=0;j<_polygons.length;j+=1){
		for (i=5;i<=30;i+=5){
			let _graphic=new PIXI.Graphics();
			_graphic.lineStyle(2,0xffffff);
			_graphic.beginFill(0x333333);
			//_graphic.drawCircle(0,0,i);
			_graphic.drawPolygon(transformPolygon(_polygons[j],i));
			textures.push(app.renderer.generateTexture(_graphic));
		}
	}

	let _graphic=new PIXI.Graphics();
	_graphic.beginFill(0xffffff);
	_graphic.drawRect(0,0,2,2);
	crawlerTextures.push(app.renderer.generateTexture(_graphic));
	crawlerTextures.push(app.renderer.generateTexture(_graphic));
	for (i=2;i<=5;i+=1){
		let _graphic=new PIXI.Graphics();
		_graphic.lineStyle(1,0x333333);
		_graphic.beginFill(0xffffff);
		_graphic.drawCircle(0,0,i);
		crawlerTextures.push(app.renderer.generateTexture(_graphic));
	}
	//trace(textures);
}

function node_GamePackage(par){
	par.onRemove=null;
	var m=new fdg_Node(par);

	//game vars
	m.health=par.health || 0.01;
	m.type=par.type || 1; //1 or greater is node
	m.powerGen=par.powerGen || 0;
	m.powerDelay=par.powerDelay || 60;
	m.powerTick=0;
	m.powerClump=(par.powerClump!=null?par.powerClump :0.1);
	m.texture=textures[m.shape*6+Math.floor(m.radius/5-1)]
	m.onTick=node_onTick;
	m.onRemove=par.onRemove || node_onRemove;
	m.tag=par.tag || NodeTags.NORMAL;
	m.fruitChain=(par.fruitChain!=null? par.fruitChain : 3);
	m.fruits=new Array;
	m.maxFruits=(par.maxFruits!=null ? par.maxFruits : 2);
	m.fruitType=par.fruitType || null;
	m.fruitSpawn=par.fruitSpawn || 0;
	m.fruitClump=par.fruitClump || 0.05;
	return m;
}

function node_makeFruitFrom(_oldNode){
	if (_oldNode===null) console.log("NO OLD NODE");
	_par=node_FruitFromType(_oldNode.type,{x:_oldNode.x,y:_oldNode.y,fruitChain:_oldNode.fruitChain-1});

	return node_GamePackage(_par);
}

function node_FruitFromType(_type,_par){
	_par=_par || {};
	_par.x=_par.x || 0;
	_par.y=_par.y || 0;
	_par.radius=_par.radius || 5;
	_par.mass=_par.mass || 0.1;
	_par.force=_par.force || 0.2;
	_par.x+=_par.radius;
	_par.y+=_par.radius;
	_par.tag=NodeTags.FRUIT;
	_par.type=(_par.type===null ? _par.type : _type);
	_par.onRemove=node_onFruitRemove;
	//_par.fruitType=_par;

	switch(_type){
		case 1: //Food
			_par.color=GameColors.ORANGE; _par.shape=1;
			break;
		case 2: //Research
			_par.color=GameColors.PURPLE; _par.shape=5;
			break;
		case 11: //Battery for Center
			_par.color=GameColors.BLUE, _par.shape=0;
			_par.maxFruits=1;
			break;
		case 4: //Generator
			_par.color=GameColors.YELLOW; _par.shape=2; _par.powerGen=0.0001;
			break;
		case 3: //Burr
			_par.color=GameColors.RED; _par.shape=2;
			break;
		case 5: //Big Evil
			_par.color=GameColors.RED; _par.shape=1; _par.radius=10; _par.mass=1; _par.force=3; _par.maxFruits=1;
			break;
		case 6: //Small Evil
			_par.color=GameColor.RED; _par.shape=3; _par.maxFruits=1;
			break;
		case 7: //Leaf
		default:
			_par.color=GameColors.GREEN; _par.shape=2;
			break;
	}
	return _par;
}

function node_NodeFromType(_type,_par){
	_par=_par || {x:0,y:0};
	switch(_type){
		case 1: _par.color=GameColors.BLUE; _par.shape=0; _par.radius=10; _par.mass=1; _par.force=1; 
			_par.fruitType=1;
			_par.fruitChain=2; _par.maxFruits=3; _par.maxLinks=1;
			break;
		case 2: _par.color=GameColors.PURPLE; _par.shape=6; _par.radius=15; _par.mass=3; _par.force=1.5;
			_par.fruitType=2;
			_par.fruitChain=1; _par.maxFruits=3; _par.maxLinks=1;
			break;
		case 3: _par.color=GameColors.YELLOW; _par.shape=5; _par.radius=20; _par.mass=10; _par.force=3;
			_par.fruitType=3;
			_par.powerGen=0.001; _par.fruitChain=1; _par.maxFruits=3; _par.maxLinks=1;
			break;
		case 4: _par.color=GameColors.ORANGE; _par.shape=1; _par.radius=10; _par.mass=1; _par.force=1;
			_par.fruitType=4;
			_par.fruitChain=1; _par.maxFruits=2; _par.maxLinks=1;
			break;
		case 5: _par.color=GameColors.RED; _par.shape=4; _par.radius=20; _par.mass=20; _par.force=4;
			_par.fruitType=5;
			_par.powerGen=0.001; _par.fruitChain=3; _par.maxFruits=3; _par.maxLinks=6;
			break;
		case 6: _par.color=GameColors.RED; _par.shape=3; _par.radius=10; _par.mass=1; _par.force=1;
			_par.fruitType=6;
			_par.fruitChain=1; _par.maxFruits=5; _par.maxLinks=4;
			break;
		case 11: _par.color=GameColors.YELLOW; _par.shape=0; _par.radius=30; _par.mass=30; _par.force=30;
			_par.fruitType=11;
			_par.powerGen=0.005; _par.fruitChain=5; _par.maxFruits=2; _par.maxLinks=5;
			break;
		case 7:
		default:_par.color=GameColors.GREEN; _par.shape=0; _par.radius=10; _par.mass=1; _par.force=1; 
			_par.fruitType=0;
			_par.fruitChain=1; _par.maxFruits=2; _par.maxLinks=4;
			_type=7;
			break;
	}

	/*switch(temp_forcedSize){
		case 1: _par.radius=5;  _par.mass=0.1;_par.force=0.2;_length=5;  _par.tag=NodeTags.FRUIT; break;
		case 2: _par.radius=10; _par.mass=1;  _par.force=1;  _length=20; break;
		case 3: _par.radius=15; _par.mass=3;  _par.force=1.5;_length=30; break;
		case 4: _par.radius=20; _par.mass=10; _par.force=3;  _length=40; break;
		case 5: _par.radius=25; _par.mass=20; _par.force=4;  _length=50; break;
		case 6: _par.radius=30; _par.mass=30; _par.force=30; _length=60; _par.powerGen=0.01; break;
	}*/
	_par.x+=_par.radius;
	_par.y+=_par.radius;
	_par.type=_type;
	return node_GamePackage(_par);
}

function node_onTick(){
	let _sprite=this.sprite;
	if (_sprite!=null){
		let intensity=0.3+0.7*Math.min(1,this.health);
		_sprite.tint=this.color*Math.floor(intensity*7);
		_sprite.width=_sprite.texture.width*intensity;
		_sprite.height=_sprite.texture.height*intensity;
	}

	if (this.powerDelay>-1){
		if (this.health<1){
			this.health+=this.powerGen;
		}
		
		this.powerTick+=1;
		if (this.powerTick>=this.powerDelay){
			this.powerTick=0;

			if (this.fruits.length>0 && this.health>=0.9){
				let _cFruit;
				for (i=0;i<this.fruits.length;i+=1){
					if (_cFruit==null || this.fruits[i].node.health<_cFruit.node.health){
						_cFruit=this.fruits[i];
					}
				}
				if (this.health>_cFruit.node.health && _cFruit.node.health<2){
					let _clump=Math.min(this.health,this.fruitClump);
					game_sendPower({link:_cFruit.link,node:this,health:_clump});
					this.health-=_clump;
				}
			}

			if (this.powerClump>0 && this.outlets.length>0 && this.health>0.25){
				let _cOutlet;
				for (var i=0;i<this.outlets.length;i+=1){
					if (_cOutlet==null || this.outlets[i].node.health<_cOutlet.node.health){
						_cOutlet=this.outlets[i];
					}
				}
				if (this.health>_cOutlet.node.health && _cOutlet.node.health<2){
					let _clump=Math.min(this.health,this.powerClump);
					game_sendPower({link:_cOutlet.link,node:this,health:_clump});
					this.health-=_clump;
				}
			}

			if (this.fruitChain>0 && this.health>=0.9 && this.fruits.length<this.maxFruits){
				this.fruitSpawn+=this.fruitClump*2;
				this.health-=this.fruitClump;
			}
		}
	}
}

function node_onRemove(){
	//this.health=-1;
	while (this.fruits.length>0){
		for (var j=0;j<this.fruits[0].node.outlets.length;j+=1){
			if (this.fruits[0].node.outlets[j].node===this){
				this.fruits[0].node.outlets.splice(j,1);
				break;
			}
		}
		this.fruits.shift();
	}
	
}

function node_onFruitRemove(){
	node_onRemove.call(this);
	
	while(this.outlets.length>0){
		for (j=0;j<this.outlets[0].node.fruits.length;j+=1){
			if (this.outlets[0].node.fruits[j].node===this){
				this.outlets[0].node.fruits.splice(j,1);
				this.outlets.shift();
				break;
			}
		}
	}
}

function node_onAdd(){
	
}

function link_gamePackage(par){
	par.onTick=node_linkOnTick;
	let m=new fdg_Link(par);
	
	m.powerClump=0;
	m.powerTick=0;
	return m;
}

function node_linkOnTick(){
	if (this.powerTick>0){
		this.powerTick-=3;
		this.intensity=2+Math.floor(this.powerTick/60*6);
	}
}