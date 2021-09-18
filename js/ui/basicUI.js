let uiM={
	currentMode:-1,
	buttons:[],
	BUTTON_SPACING:45,
	BUTTON_CENTER_X:220,
};
let oldDate=0;

var temp_time1=0;
var temp_time2=0;
var temp_other=0;

function ui_init(){
	uiM.bottomBar=new window_bottomBar();

	uiM.node=node_GamePackage({x:30,y:30,health:1,powerDelay:-1,fixed:true});
	uiM.node.makeSprite();
	uiM.node.updateSprite();
	ui_addSelectButton("Move",8);
	ui_addSelectButton("Delete",9);
	uiM.buttons.push(null);
	ui_addSelectButton("Stem",7);
	ui_addSelectButton("Flower",1);///*INDEX*/);
	ui_addSelectButton("Research",2);
	ui_addSelectButton("Power",3);
	ui_addSelectButton("Home",4);
	ui_addSelectButton("Crawl",10);

	ui_addToggleButton("Turbo",game_turboMode,false);
	//ui_addSelectButton("Cube",5);
	//ui_addSelectButton("Sliver",6);
	
	
	ui_selectButtonAt(0);
	/*let _button=button_smallButton("Point",ui_selectButton);
	_button.x=40;
	_button.y=20;
	ui_addButton(_button);
	uiM.bottomBar.addChild(_button);*/

	uiM.bottomBar.addChild(uiM.node.sprite);
	app.stage.addChild(uiM.bottomBar);
	app.ticker.add(ui_onTick);
}


function ui_onTick(_delta){
	if (uiM.node.type!=temp_type){
		uiM.node.sprite.destroy();
		uiM.node.sprite=null;
		uiM.node=null;
	}
	if (uiM.node===null){
		uiM.node=node_NodeFromType(temp_type,{x:40,y:40,health:1,powerDelay:-1,fixed:true});
		uiM.node.makeSprite(textures[uiM.node.shape*6+Math.floor(uiM.node.radius/5-1)]);
		uiM.node.updateSprite();
		uiM.bottomBar.addChild(uiM.node.sprite);
		uiM.node.type=temp_type;
	}
	
	uiM.node.onTickMain();
	uiM.node.updateSprite();
	
	//uiM.bottomBar.text.text="G: "+temp_time1+"\nP: "+temp_time2+"\nNumObj: "+gameM.forces.nodes.length+"\nO:"+temp_other;
	uiM.bottomBar.text.text="Nodes: "+plantM.numNodes+"/"+plantM.maxNodes+"\nCrawlers: "+plantM.numCrawlers+"/"+plantM.maxCrawlers;
}

function ui_addToggleButton(_name,_function,_toggled=false){
	let _button=button_smallButton(_name,toggleToggler);
	_button.toggled=_toggled;
	_button.setSelectState(_toggled);
	_button.output2=_function;
	
	_button.x=STAGE_WIDTH-50;
	_button.y=20;
	uiM.bottomBar.addChild(_button);

	return _button;

}

function toggleToggler(){
	this.toggled=!this.toggled;
	this.setSelectState(this.toggled);
	this.output2(this.toggled);
	return this.toggled;
}

function ui_addButton(_button){
	_button.index=uiM.buttons.length;

	uiM.buttons.push(_button);
}

function ui_addSelectButton(_name,_index){
	let _button=button_smallButton(_name,ui_selectButton);
	_button.y=20;
	uiM.bottomBar.addChild(_button);
	_button.index=_index;
	uiM.buttons.push(_button);
	let _startX=Math.max(uiM.BUTTON_CENTER_X-Math.floor(uiM.buttons.length/2)*uiM.BUTTON_SPACING,60);
	for (var i=0;i<uiM.buttons.length;i+=1){
		if (uiM.buttons[i]!=null){
			uiM.buttons[i].x=_startX+i*uiM.BUTTON_SPACING;
		}
	}
}

function ui_selectButton(){
	if (uiM.currentMode!=this.index){
		uiM.currentMode=this.index;
		for (var i=0;i<uiM.buttons.length;i+=1){
			if (uiM.buttons[i]!=null) uiM.buttons[i].setSelectState(uiM.buttons[i]===this);
		}
		EventManager.registerEvent(EventManagerTypes.UI_SELECT_MODE,new EventManager_UISelectModeEvent(uiM.currentMode));
	}
}

function ui_selectButtonAt(i){
	if (i>=uiM.buttons.length) return;
	
	if (uiM.buttons[i]!=null) ui_selectButton.call(uiM.buttons[i]);
}


function uiElement_constructor(par){
	//required: width, height
	//optional: bgColor, label, 
	//optional: clickFunction, clickRemove
	var m=new PIXI.Sprite();
	m.graphics=new PIXI.Graphics();
	m.addChild(m.graphics);
	if (par.bgColor==null){
		m.graphics.beginFill(0x808080);
	}else{
		m.graphics.beginFill(par.bgColor);
	}
	
	m.graphics.drawRect(0,0,par.width,par.height);
	if (par.label!=null){
		m.label=new PIXI.Text(par.label,{fill:0xffffff});
		m.label.x=par.width/15;
		m.label.y=par.height/15;
		m.addChild(m.label);
	}

	if (par.clickFunction!=null){
		m.interactive=true;
		m.clickFunction=par.clickFunction;
		m.on("pointerdown",m.clickFunction);
	}
	if (par.x!=null) m.x=par.x;
	if (par.y!=null) m.y=par.y;
	if (par.alpha!=null) m.graphics.alpha=par.alpha;
	return m;
}