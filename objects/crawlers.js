
var crawler_powerTexture;
var crawler_defaultTexture;

function crawlers_init(){
	let m=new PIXI.Graphics();
	m.beginFill(0xffffff);
	m.drawRect(0,0,2,2);
	crawler_powerTexture=app.renderer.generateTexture(m);

	m=new PIXI.Graphics();
	m.lineStyle(1,0x333333);
	m.beginFill(0xffffff);
	m.drawCircle(0,0,3);
	crawler_defaultTexture=app.renderer.generateTexture(m);
}

function crawl_CrawlerManager(par){
	par=par || {};
	this.canvas=par.canvas || null;
	this.crawlers=par.crawlers || new Array();
	
	this.addCrawler=function(par){
		par=par || {};
		par.canvas=par.canvas || this.canvas;
		par.texture=par.texture || crawler_defaultTexture;
		let m = new crawl_BaseCrawler(par);
		this.crawlers.push(m);

		let _sprite=m.makeSprite(par.texture);
		if (par.type==-2){
			par.canvas.addChildAt(_sprite,2);
		}else{
			par.canvas.addChild(_sprite);
		}
		return m;
	}

	this.removeCrawler=function(_crawler){
		for (var i=0;i<this.crawlers.length;i+=1){
			if (this.crawlers[i]===_crawler){
				this.removeCrawlerByIndex(i);
				return;
			}
		}
	}

	this.removeCrawlerByIndex=function(i){
		if (this.crawlers[i].sprite!=null){
			this.crawlers[i].sprite.parent.removeChild(this.crawlers[i].sprite);
			this.crawlers[i].sprite.destroy();
		}
		this.crawlers.splice(i,1);
	}

	this.nodeRemoved=function(_node){
		for (var i=0;i<this.crawlers.length;i+=1){
			if (this.crawlers[i].cLoc.type>0){
				if (this.crawlers[i].cLoc===_node){
					this.removeCrawlerByIndex(i);
					i-=1;
				}
			}else if (this.crawlers[i].cLoc.type===0){
				if (this.crawlers[i].cLoc.origin===_node || this.crawlers[i].cLoc.target===_node){
					this.removeCrawlerByIndex(i);
					i-=1;
				}
			}
		}
	}

	this.addPowerCell=function(par){
		let _node=par.node;
		let _link=par.link;
		let m={
			type:-2,
			aiType:3,
			health:par.health,
			cLoc:par.link,
			mag:_node===_link.origin ? 0 : 1,
			radius:1,
			color:0xffffff,
			speed:_node===_link.origin ? 0.1 : -0.1,
			texture:crawler_powerTexture
		}
		this.addCrawler(m);
	}

	this.updateCrawlers=function(_andSprite){
		for (var i=0;i<this.crawlers.length;i+=1){
			let _crawler=this.crawlers[i];
			switch(_crawler.aiType){
				case 0: _crawler.mag+=_crawler.speed/_crawler.cLoc.length*40;
						if (_crawler.mag>1){
							_crawler.mag=0;
							_crawler.cLoc=_crawler.cLoc.target;
							_crawler.setAI();
						}else if (_crawler.mag<0){
							_crawler.mag=0;
							_crawler.cLoc=_crawler.cLoc.origin;
							_crawler.setAI();
						}
						break;
				case 1: _crawler.mag+=_crawler.speed/_crawler.cLoc.radius*10;
						if (_crawler.mag>_crawler.aiExtra){
							_crawler.mag=_crawler.aiExtra;
							_crawler.speed=-_crawler.speed;
						}else if (_crawler.mag<0){
							_crawler.mag=0;
							_crawler.setAI();
						}
						break;
				case 2: _crawler.mag-=_crawler.speed/_crawler.cLoc.radius*10;
						if (_crawler.mag<0){
							_crawler.mag=0;
							_crawler.setAI();
						}
						break;
				case 3: _crawler.mag+=_crawler.speed/_crawler.cLoc.length*40;
						if (_crawler.mag>1){
							_crawler.cLoc.target.health+=_crawler.health;
							this.removeCrawler(_crawler);
							return;
						}else if (_crawler.mag<0){
							_crawler.cLoc.origin.health+=_crawler.health;
							this.removeCrawler(_crawler);
							return;
						}
						break;
			}
			
				/*if (_crawler.mag<_crawler.aiExtra){
					_crawler.mag+=_crawler.speed/_crawler.cLoc.radius*10;
				}else{
					_crawler.mag=_crawler.aiExtra;
				}
				if (_crawler.aiExtra1>0){
					let _amt=Math.min(_crawler.aiExtra1,_crawler.speed/10);
					_crawler.aiExtra1-=_amt;
					_crawler.angle-=_amt;
					//trace(_crawler.aiExtra1);
				}else if (_crawler.aiExtra1<0){
					let _amt=Math.max(_crawler.aiExtra1,_crawler.speed/10);
					_crawler.aiExtra1-=_amt;
					_crawler.angle-=_amt;
				//	trace(_crawler.aiExtra1);
				}
				if (_crawler.mag==_crawler.aiExtra && _crawler.aiExtra1==0){
					trace("A");
					_crawler.setAI();
				}*/
			if (_andSprite) _crawler.updateSprite();
		}
	}
}


function crawl_BaseCrawler(par){
	par=par || {};
	this.radius=par.radius || 3;
	this.color=par.color || 0x001524;

	this.mag=par.mag || 0;
	this.ang=par.ang || 0;
	this.speed=par.speed || 0.01;

	this.cLoc=par.cLoc || null;

	this.type=par.type || -1; //-1 or lower is Crawler; -1 is Basic, -2 is Power Point
	this.aiType=par.aiType || 0; //0 - walk, 1 - idle in node, 2 - return to node center, 3 - move and suicide
	this.health=par.health || 0.01;

	this.aiExtra=0;
	this.aiExtra1=0;

	this.setAI=function(i=-1){
		if (i==-1) i=Math.floor(Math.random()*2);
		this.aiType=i;
		switch(i){
			case 0:
				let _node=this.cLoc;
				if (this.cLoc.outlets.length===0){
					this.setAI();
					return;
				}
				let a=crawlers_findNotFood(this.cLoc.outlets);
				if (a.length===0){
					this.setAI();
					return;
				}
				this.cLoc=a[Math.floor(Math.random()*a.length)].link;
				if (_node===this.cLoc.origin){
					this.mag=0;
					this.speed=Math.abs(this.speed);
				}else{
					this.mag=1;
					this.speed=-Math.abs(this.speed);
				}
				break;
			case 1:
				//this.mag=0;
				this.ang=-Math.PI+2*Math.PI*Math.random();
				this.aiExtra=0.3+Math.random()*0.7;
				this.aiExtra1=-Math.PI/4+Math.PI/2*Math.random();
				if (this.aiExtra>this.mag){
					this.speed=Math.abs(this.speed);
				}else{
					this.speed=-Math.abs(this.speed);
				}
				break;
			case 2:
				//this.mag=0;
				this.speed=Math.abs(this.speed);
		}
	}

	this.makeSprite=function(_texture){
		this.sprite=new PIXI.Sprite(_texture);
		this.sprite.tint=this.color;
	//	if (this.type!=-2){
			this.sprite.pivot.x=this.sprite.width*0.5;
			this.sprite.pivot.y=this.sprite.height*0.5;
	//	}
		return this.sprite;
	}

	this.updateSprite=function(){
		let _sprite=this.sprite;
		if (_sprite==null || this.cLoc==null) return;
		let _x;
		let _y;

		if (this.cLoc.type==0){ //on a Link
			_x=this.cLoc.origin.x+(this.cLoc.target.x-this.cLoc.origin.x)*this.mag;
			_y=this.cLoc.origin.y+(this.cLoc.target.y-this.cLoc.origin.y)*this.mag;
		}else if (this.cLoc.type>0){ // on a Node
			_x=this.cLoc.x+this.mag*this.cLoc.radius*Math.cos(this.ang);
			_y=this.cLoc.y+this.mag*this.cLoc.radius*Math.sin(this.ang);
		}else{
			return;
		}

		this.sprite.x=_x;//-_node.radius*intensity;
		this.sprite.y=_y;//-_node.radius*intensity;
	}
}

function crawlers_findNotFood(a){
		let m=[];
		for (var i=0;i<a.length;i+=1){
			if (a[i].node.tag!=NodeTags.FRUIT){
				m.push(a[i]);
			}
		}
		return m;
	}