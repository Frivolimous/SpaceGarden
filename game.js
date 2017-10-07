var gameM={
	forces:null,
	crawlers:null,
	gameStage:new PIXI.Sprite(),
	background:new PIXI.Graphics(),
	paralax:null,
	paralax2:null,
	PARALAX_RATIO:4/5,
	backgroundScroller:{vX:0,vY:0,x:0,y:0},
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
	plantM.reset();

	inputM.dragFunction=game_onMouseDown;
	inputM.gameCanvas=gameM.gameStage;
	//let _justBlack=makeStarfield(Config.GRID_SIZE,gameM.gameBounds.right,gameM.gameBounds.bot,0x999999/*,GameColors.BACKGROUND*/);
	gameM.paralax2=makeStarfield(Config.GRID_SIZE*gameM.PARALAX_RATIO*gameM.PARALAX_RATIO,gameM.gameBounds.right*gameM.PARALAX_RATIO*gameM.PARALAX_RATIO,gameM.gameBounds.bot*gameM.PARALAX_RATIO*gameM.PARALAX_RATIO,0x666666,GameColors.BACKGROUND);
	gameM.paralax=makeStarfield(Config.GRID_SIZE*gameM.PARALAX_RATIO,gameM.gameBounds.right*gameM.PARALAX_RATIO,gameM.gameBounds.bot*gameM.PARALAX_RATIO,0x999999);

	/*let _justBlack=makePrecisionGrid(Config.GRID_SIZE,gameM.gameBounds.right,gameM.gameBounds.bot,GameColors.GRID/*,GameColors.BACKGROUND);
	gameM.paralax=makePrecisionGrid(Config.GRID_SIZE*gameM.PARALAX_RATIO,gameM.gameBounds.right*gameM.PARALAX_RATIO,gameM.gameBounds.bot*gameM.PARALAX_RATIO,GameColors.GRID-0x111111,GameColors.BACKGROUND);
	*/
	//=new PIXI.Graphics();
	/*_justBlack.beginFill(0);
	_justBlack.drawRect(0,0,gameM.gameBounds.right,gameM.gameBounds.bot);*/
	//gameM.gameStage.addChild(_justBlack);
	gameM.gameStage.addChild(gameM.background);
	
	app.stage.addChild(gameM.paralax2);
	app.stage.addChild(gameM.paralax);

	app.stage.addChild(gameM.gameStage);
	gameM.forces=new fdg_ForceContainer({canvas:gameM.gameStage,bgCanvas:gameM.background,borders:gameM.gameBounds});
	gameM.crawlers=new crawl_CrawlerManager({canvas:gameM.gameStage});

	let _newNode=node_NodeFromType(11,{x:200,y:200,canvas:gameM.gameStage});
	gameM.forces.addNode(_newNode);
	EventManager.addEventListener(EventManagerTypes.UI_SELECT_MODE,changeBuildType);
	/*var _node=node_GamePackage({x:100,y:100,health:1});
	gameM.forces.addNode(_node);*/
	/*gameM.forces.addNewNode({x:120,y:100,health:1});
	gameM.forces.addLink({origin:gameM.forces.nodes[0],target:gameM.forces.nodes[1]});
	gameM.crawlers.addCrawler({cLoc:gameM.forces.links[0]});*/
	/*for (var i=1;i<1000;i+=1){
		gameM.forces.addNewNode({x:i,health:1,shape:2});
		gameM.forces.addLink({origin:gameM.forces.nodes[i-1],target:gameM.forces.nodes[i]});
	}*/
	
	
	app.ticker.add(game_onTick);

	running=true;
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
	if (!running) return;
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
/*
	if (keyStates.left) gameM.gameStage.x+=5;
	if (keyStates.right) gameM.gameStage.x-=5;
	if (keyStates.up) gameM.gameStage.y+=5;
	if (keyStates.down) gameM.gameStage.y-=5;*/


	if (keyStates.left) gameM.backgroundScroller.vX+=1;
	if (keyStates.right) gameM.backgroundScroller.vX-=1;
	if (keyStates.up) gameM.backgroundScroller.vY+=1;
	if (keyStates.down) gameM.backgroundScroller.vY-=1;
	//console.log(gameM.backgroundScroller.vX);
	if (gameM.backgroundScroller.vX!=0 || gameM.backgroundScroller.vY!=0){
		gameM.gameStage.x+=gameM.backgroundScroller.vX;
		gameM.gameStage.y+=gameM.backgroundScroller.vY;
		gameM.backgroundScroller.vX*=0.9;
		gameM.backgroundScroller.vY*=0.9;
		if (Math.abs(gameM.backgroundScroller.vX)<0.001 && Math.abs(gameM.backgroundScroller.vY)<0.001){
			gameM.backgroundScroller.vX=0;
			gameM.backgroundScroller.vY=0;
		}
		//console.log(gameM.backgroundScroller);
	}

	if (gameM.gameStage.x>0) gameM.gameStage.x=1;
	if (gameM.gameStage.y>0) gameM.gameStage.y=0;
	
	let _num=stageBorders.right-gameM.gameBounds.right*gameM.scale;
	if (gameM.gameStage.x<_num) gameM.gameStage.x=_num;
	gameM.paralax.x=gameM.gameStage.x/_num*(stageBorders.right-gameM.paralax.width*gameM.scale);
	gameM.paralax2.x=gameM.gameStage.x/_num*(stageBorders.right-gameM.paralax2.width*gameM.scale);


	_num=stageBorders.bot-gameM.gameBounds.bot*gameM.scale;
	if (gameM.gameStage.y<_num) gameM.gameStage.y=_num;
	gameM.paralax.y=gameM.gameStage.y/_num*(stageBorders.bot-gameM.paralax.height*gameM.scale);
	gameM.paralax2.y=gameM.gameStage.y/_num*(stageBorders.bot-gameM.paralax2.height*gameM.scale);
	//let _backLeftMax=
	
	//gameM.paralax.y=gameM.gameStage.y/gameM.gameStage.height*gameM.paralax.height;
	//console.log(gameM.paralax.x+" "+gameM.paralax.width+" "+gameM.gameBounds.right);
}

function game_zoom(n){
	gameM.scale*=n;
	if (gameM.scale>3) gameM.scale=3;
	var _boundRatio=Math.min(stageBorders.right/gameM.gameBounds.right,stageBorders.bot/gameM.gameBounds.bot);
	if (gameM.scale<_boundRatio) gameM.scale=_boundRatio;
	gameM.scale=Math.max(0.3,Math.min(3,gameM.scale));
	gameM.gameStage.scale.x=gameM.gameStage.scale.y=gameM.scale;
}

function game_turboMode(b){
	if (b) gameM.gameRate=10;
	else gameM.gameRate=1;
}

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
		let _object=gameM.forces.getClosestObject({x:e.gameX,y:e.gameY,notHasTag:NodeTags.FRUIT});
		gameM.backgroundScroller.x=e.gameX;
		gameM.backgroundScroller.y=e.gameY;
		//gameM.backgroundScroller.start={x:e.gameX,y:e.gameY};
		return _object || gameM.backgroundScroller;

	}else if (temp_type==9){
		let _node=gameM.forces.getClosestObject({x:e.gameX,y:e.gameY});
		if (_node==null || _node.radius>=30) return;
		plantM.numNodes-=1;
		gameM.crawlers.nodeRemoved(_node);
		gameM.forces.removeNode(_node);
	}else if (temp_type==10){
		if (plantM.numCrawlers>=plantM.maxCrawlers) return;
		var _node=gameM.forces.getClosestObject({x:e.gameX,y:e.gameY,notHasTag:NodeTags.FRUIT});
		if (_node!=null){
			gameM.crawlers.addCrawler({cLoc:_node,aiType:1,color:GameColors.ORANGE*7});
			plantM.numCrawlers+=1;
		}
	}else{
		if (plantM.numNodes>=plantM.maxNodes) return;
		let _oldNode=gameM.forces.getClosestObject({x:e.gameX,y:e.gameY,distance:100,maxLinks:true,notHasTag:NodeTags.FRUIT});
		if (_oldNode===null) return;

		let _newNode=node_NodeFromType(temp_type,{x:e.gameX,y:e.gameY});
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
		let _oldNode=gameM.forces.getClosestObject({x:e.gameX,y:e.gameY,distance:100,maxLinks:true,notHasTag:NodeTags.FRUIT});
		if (_oldNode===null) return;

		let _newNode=node_NodeFromType(temp_type,{x:e.gameX,y:e.gameY});
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
		let _node=gameM.forces.getClosestObject({x:e.gameX,y:e.gameY});
		if (_node==null || _node.radius>=30) return;
		plantM.numNodes-=1;
		gameM.crawlers.nodeRemoved(_node);
		gameM.forces.removeNode(_node);
		
	}else{
		return gameM.forces.getClosestObject({x:e.gameX,y:e.gameY,notHasTag:NodeTags.FRUIT});
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

function makePrecisionGrid(_size,_width,_height,_color,_background=null){
	let _grid=new PIXI.Graphics();
	if (_background!=null){
		_grid.beginFill(_background);
		_grid.drawRect(0,0,_width,_height);
		_grid.endFill();
	}
	_grid.lineStyle(1,_color);
	for (var i=_size;i<_width;i+=_size){
		_grid.moveTo(i,0);
		_grid.lineTo(i,_height);
	}
	for (i=_size;i<_height;i+=_size){
		_grid.moveTo(0,i);
		_grid.lineTo(_width,i);
	}
	return _grid;
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