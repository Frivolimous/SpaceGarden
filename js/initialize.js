
//== Main Initialization ==\\
var interactionMode="desktop";
var _Resolution=1;
try{
	document.createEvent("TouchEvent");
	interactionMode="mobile";
	/*console.log(STAGE_WIDTH+" "+STAGE_HEIGHT+" "+window.innerWidth+" "+window.innerHeight);
	let _ratio=Math.max(window.innerWidth/STAGE_WIDTH,window.innerHeight/STAGE_HEIGHT);*/
	STAGE_WIDTH=window.innerWidth;
	STAGE_HEIGHT=window.innerHeight;
	/*console.log(_ratio,_Resolution);
	while(_ratio>2){
		_ratio/=2;
		_Resolution+=1;
		STAGE_WIDTH/=2;
		STAGE_HEIGHT/=2;
	}*/

}catch(e){
	//interactionMode="desktop";
}

var app = new PIXI.Application(STAGE_WIDTH,STAGE_HEIGHT,{
	backgroundColor:0xff0000,
	antialias:true,
	resolution:_Resolution,
	roundPixels:true,
});

document.getElementById("game-canvas").append(app.view);

//== Initialize Variables for use ==\\

//keybard, mobile, mouse
let stageBorders={left:app.view.offsetLeft,top:app.view.offsetTop,right:STAGE_WIDTH,bot:STAGE_HEIGHT};


//== Initialize Supporting Structures ==\\
app.stage.interactive=true;
window.addEventListener("resize",function(){
	stageBorders.left=app.view.offsetLeft;
	stageBorders.top=app.view.offsetTop;
});
EventManager_init();

//== Initialize Game Elements ==\\

fdg_init();
crawlers_init();
nodes_init();
input_init();


//== Utility Functions ==\\
// (Call These)


//== Support Functions ==\\
// (Don't Call These)

//== Initialize the game after everything is setup ==\\
game_init();
ui_init();