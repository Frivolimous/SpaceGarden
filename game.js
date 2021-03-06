var gameM={
	forces:null,
	crawlers:null,
	gameStage:new PIXI.Sprite(),
	background:new PIXI.Graphics(),
	paralax:null,
	paralax2:null,
	PARALAX_RATIO:4/5,
	backgroundScroller:{vX:0,vY:0,x:0,y:0,target:null},
	scale:1,
	gameBounds:{left:0,top:0,bot:1500,right:1500},
	gameRate:1,
	buildType:-1
};

var plantM={
	numNodes:0,
	numCrawlers:0,
	maxNodes:16,
	maxCrawlers:5,
	reset:function(){
		this.numNodes=0;
		this.numCrawlers=0;
		this.maxNodes=16;
		this.maxCrawlers=5;
	}
}

//== Initialize Game Elements==\\
function game_init(){
	inputM.dragFunction=game_onMouseDown;
	inputM.gameCanvas=gameM.gameStage;

	gameM.gameBounds.bot=Math.max(gameM.gameBounds.bot,stageBorders.bot);
	gameM.gameBounds.right=Math.max(gameM.gameBounds.right,stageBorders.right);

	gameM.paralax=makeStarfield(Config.GRID_SIZE*gameM.PARALAX_RATIO,Math.max(gameM.gameBounds.right*gameM.PARALAX_RATIO,stageBorders.right),Math.max(gameM.gameBounds.bot*gameM.PARALAX_RATIO,stageBorders.bot),0x999999);
	gameM.paralax2=makeStarfield(Config.GRID_SIZE*gameM.PARALAX_RATIO*gameM.PARALAX_RATIO,Math.max(gameM.gameBounds.right*gameM.PARALAX_RATIO*gameM.PARALAX_RATIO,stageBorders.right),Math.max(gameM.gameBounds.bot*gameM.PARALAX_RATIO*gameM.PARALAX_RATIO,stageBorders.bot),0x666666,GameColors.BACKGROUND);
	
	gameM.gameStage.addChild(gameM.background);
	app.stage.addChild(gameM.paralax2);
	app.stage.addChild(gameM.paralax);
	app.stage.addChild(gameM.gameStage);

	plantM.reset();	
	
	gameM.forces=new fdg_ForceContainer({canvas:gameM.gameStage,bgCanvas:gameM.background,borders:gameM.gameBounds});
	gameM.crawlers=new crawl_CrawlerManager({canvas:gameM.gameStage});

	let _newNode=node_NodeFromType(11,{x:200,y:200,canvas:gameM.gameStage});
	gameM.forces.addNode(_newNode);
	EventManager.addEventListener(EventManagerTypes.UI_SELECT_MODE,changeBuildType);
	EventManager.addEventListener(EventManagerTypes.DRAG_EVENT,game_onDrag);
	app.ticker.add(game_onTick);

	running=true;
}

function game_onDrag(e){
	if (e.startDrag){
		gameM.forces.addPull(e.drag);
	}else{
		gameM.forces.removePull(e.drag);
	}
}

function game_addRandomCrawler(){
	if (plantM.numCrawlers>=plantM.maxCrawlers) return;
	var _node;
	do{
		var _node=gameM.forces.nodes[Math.floor(Math.random()*gameM.forces.nodes.length)];
	}while(_node.tag==NodeTags.FRUIT);

	gameM.crawlers.addCrawler({cLoc:_node,aiType:1,color:temp_forcedColor*7});
	plantM.numCrawlers+=1;
}

//== Start/Stop the Game ==\\
function restartGame(){
	startGame();
}

function startGame(){
	running=true;
}

function game_lose(){
	running=false;
	app.stage.addChild(window_construct_lose(startGame));
}

//==Primary Game Loop==\\
function game_onTick(e){
	if (running){
		for (var run=gameM.gameRate;run>0;run--){
			
			gameM.forces.applyForces(run===1);

			gameM.crawlers.updateCrawlers(run===1);		
			
			for (var i=0;i<gameM.forces.nodes.length;i+=1){
				if (gameM.forces.nodes[i].fruitSpawn>1 && gameM.forces.nodes[i].fruits.length<gameM.forces.nodes[i].maxFruits){
					game_spawnFruit(gameM.forces.nodes[i]);
				}
			}
		}

		gameM.forces.drawLinks();
	}

	game_navigate();
}

function game_turboMode(b){
	if (b) gameM.gameRate=10;
	else gameM.gameRate=1;
}

//==Interactions

function game_spawnFruit(_oldNode){
	_oldNode.fruitSpawn-=1;
	let _node=node_makeFruitFrom(_oldNode);
	
	gameM.forces.addNode(_node);

	let _link=link_gamePackage({origin:_oldNode,target:_node,length:(_oldNode.radius+_node.radius),color:_node.color,noOutlets:true,powerClump:0});
	gameM.forces.addLink(_link);
	_oldNode.fruits.push({link:_link,node:_node});
	_node.outlets.push({link:_link,node:_oldNode});

	return _node;
}

function game_onMouseDown(e){
	if (temp_type==8){
		let _object=gameM.forces.getClosestObject({x:e.x,y:e.y,notHasTag:NodeTags.FRUIT});
		if (_object!=null){
			return _object;
		}else{
			gameM.backgroundScroller.target=e;
			/*gameM.backgroundScroller.x=gameM.gameStage.x;
			gameM.backgroundScroller.y=gameM.gameStage.y;*/
			gameM.backgroundScroller.x=e.stageX-gameM.gameStage.x;
			gameM.backgroundScroller.y=e.stageY-gameM.gameStage.y;
		}
		/*gameM.backgroundScroller.x=e.x;
		gameM.backgroundScroller.y=e.y;
		return _object || gameM.backgroundScroller;*/

	}else if (temp_type==9){
		let _node=gameM.forces.getClosestObject({x:e.x,y:e.y});
		if (_node==null || _node.radius>=30) return;
		plantM.numNodes-=1;
		gameM.crawlers.nodeRemoved(_node);
		gameM.forces.removeNode(_node);
	}else if (temp_type==10){
		if (plantM.numCrawlers>=plantM.maxCrawlers) return;
		var _node=gameM.forces.getClosestObject({x:e.x,y:e.y,notHasTag:NodeTags.FRUIT});
		if (_node!=null){
			gameM.crawlers.addCrawler({cLoc:_node,aiType:1,color:GameColors.ORANGE*7});
			plantM.numCrawlers+=1;
		}
	}else{
		if (plantM.numNodes>=plantM.maxNodes) return;
		let _oldNode=gameM.forces.getClosestObject({x:e.x,y:e.y,distance:100,maxLinks:true,notHasTag:NodeTags.FRUIT});
		if (_oldNode===null) return;

		let _newNode=node_NodeFromType(temp_type,{x:e.x,y:e.y});
		gameM.forces.addNode(_newNode);
		plantM.numNodes+=1;

		if (_oldNode!=null){
			_length=(_oldNode.radius+_newNode.radius)*1.5;
			//gameM.forces.addNewLink({origin:_oldNode,target:_newNode,length:_length,color:_newNode.color});
			let _link=link_gamePackage({origin:_oldNode,target:_newNode,length:_length,color:_newNode.color});
			gameM.forces.addLink(_link);
		}
		return _newNode;
	}
	/*if (e.ctrlKey){
		if (plantM.numNodes>=plantM.maxNodes) return;
		let _oldNode=gameM.forces.getClosestObject({x:e.x,y:e.y,distance:100,maxLinks:true,notHasTag:NodeTags.FRUIT});
		if (_oldNode===null) return;

		let _newNode=node_NodeFromType(temp_type,{x:e.x,y:e.y});
		gameM.forces.addNode(_newNode);
		plantM.numNodes+=1;

		if (_oldNode!=null){
			_length=(_oldNode.radius+_newNode.radius)*1.5;
			//gameM.forces.addNewLink({origin:_oldNode,target:_newNode,length:_length,color:_newNode.color});
			let _link=link_gamePackage({origin:_oldNode,target:_newNode,length:_length,color:_newNode.color});
			gameM.forces.addLink(_link);
		}
		return _newNode;
	}else if (e.shiftKey){
		let _node=gameM.forces.getClosestObject({x:e.x,y:e.y});
		if (_node==null || _node.radius>=30) return;
		plantM.numNodes-=1;
		gameM.crawlers.nodeRemoved(_node);
		gameM.forces.removeNode(_node);
		
	}else{
		return gameM.forces.getClosestObject({x:e.x,y:e.y,notHasTag:NodeTags.FRUIT});
	}*/
}

function game_sendPower(par){
	par.link.intensity=9;
	par.link.powerTick=60;
	if (par.node==par.link.origin) par.link.target.health+=par.health;
	else par.link.origin.health+=par.health;

	//gameM.crawlers.addPowerCell(par);
}

function changeBuildType(e){
	gameM.buildType=e.mode;
}

function makeStarfield(_size,_width,_height,_color,_background=null){
	let NUM_STARS=500;
	let _grid=new PIXI.Graphics();
	if (_background!=null){
		_grid.beginFill(_background);
		_grid.drawRect(0,0,_width,_height);
		_grid.endFill();
	}
	_grid.beginFill(_color);
	for (var i=0;i<NUM_STARS;i+=1){
		_grid.drawRect(Math.random()*_width,Math.random()*_height,1+Math.random()*1,1+Math.random()*1);
	}
	return _grid;
}

//===NAVIGATION CONTROLS===

function game_navigate(){
	if (gameM.backgroundScroller.target!=null){
		if (!gameM.backgroundScroller.target.down){
			gameM.backgroundScroller.target=null;
		}else{
			gameM.backgroundScroller.vX=(gameM.backgroundScroller.target.stageX-gameM.backgroundScroller.x - gameM.gameStage.x)/15;
			gameM.backgroundScroller.vY=(gameM.backgroundScroller.target.stageY-gameM.backgroundScroller.y - gameM.gameStage.y)/15;
			/*gameM.backgroundScroller.vX=-(gameM.backgroundScroller.target.stageX-gameM.backgroundScroller.x)/2;
			gameM.backgroundScroller.vY=-(gameM.backgroundScroller.target.stageY-gameM.backgroundScroller.y)/2;*/
		}
	}else{
		if (keyStates.left) gameM.backgroundScroller.vX+=1;
		if (keyStates.right) gameM.backgroundScroller.vX-=1;
		if (keyStates.up) gameM.backgroundScroller.vY+=1;
		if (keyStates.down) gameM.backgroundScroller.vY-=1;
	}
	if (gameM.backgroundScroller.vX!=0 || gameM.backgroundScroller.vY!=0){
		gameM.gameStage.x+=gameM.backgroundScroller.vX;
		gameM.gameStage.y+=gameM.backgroundScroller.vY;
		gameM.backgroundScroller.vX*=0.95;
		gameM.backgroundScroller.vY*=0.95;
		if (Math.abs(gameM.backgroundScroller.vX)<0.001 && Math.abs(gameM.backgroundScroller.vY)<0.001){
			gameM.backgroundScroller.vX=0;
			gameM.backgroundScroller.vY=0;
		}
	}

	if (gameM.gameStage.x>0) gameM.gameStage.x=1;
	if (gameM.gameStage.y>0) gameM.gameStage.y=0;
	
	let _num=stageBorders.right-gameM.gameBounds.right*gameM.scale;
	if (gameM.gameStage.x<_num) gameM.gameStage.x=_num;
	if (_num==0){
		gameM.paralax.x=0;
		gameM.paralax2.x=0;
	}else{
		
		gameM.paralax.x=(stageBorders.right-gameM.paralax.width)*gameM.gameStage.x/_num || 0;
		gameM.paralax2.x=(stageBorders.right-gameM.paralax2.width)*gameM.gameStage.x/_num || 0;
		/*gameM.paralax.x=(gameM.gameStage.x/_num*(stageBorders.right-gameM.paralax.width))*gameM.scale || 0;
		gameM.paralax2.x=(gameM.gameStage.x/_num*(stageBorders.right-gameM.paralax2.width))*gameM.scale || 0;*/
	}

	_num=stageBorders.bot-gameM.gameBounds.bot*gameM.scale;
	if (gameM.gameStage.y<_num) gameM.gameStage.y=_num;
	if (_num==0){
		gameM.paralax.y=0;
		gameM.paralax2.y=0;
	}else{
		gameM.paralax.y=(stageBorders.bot-gameM.paralax.height)*gameM.gameStage.y/_num || 0;
		gameM.paralax2.y=(stageBorders.bot-gameM.paralax2.height)*gameM.gameStage.y/_num || 0;
		/*gameM.paralax.y=(gameM.gameStage.y/_num*(stageBorders.bot-gameM.paralax.height))*gameM.scale || 0;
		gameM.paralax2.y=(gameM.gameStage.y/_num*(stageBorders.bot-gameM.paralax2.height))*gameM.scale|| 0;*/
	}
}

function game_zoomBy(n){
	gameM.scale*=n;
	game_checkZoom();
}

function game_checkZoom(){
	if (gameM.scale>3) gameM.scale=3;
	var _boundRatio=Math.min(stageBorders.right/gameM.gameBounds.right,stageBorders.bot/gameM.gameBounds.bot);
	if (gameM.scale<_boundRatio) gameM.scale=_boundRatio;
	gameM.scale=Math.max(0.3,Math.min(3,gameM.scale));
	gameM.gameStage.scale.x=gameM.gameStage.scale.y=gameM.scale;
}

function game_zoomTo(n){
	gameM.scale=n;
	game_checkZoom();
}