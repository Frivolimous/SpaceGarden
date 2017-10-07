
//== Main Initialization ==\\
var interactionMode="desktop";

try{
	document.createEvent("TouchEvent");
	interactionMode="mobile";
	STAGE_WIDTH=window.innerWidth;
	STAGE_HEIGHT=window.innerHeight;
}catch(e){
	//interactionMode="desktop";
}

var app = new PIXI.Application(STAGE_WIDTH,STAGE_HEIGHT,{
	backgroundColor:0xff0000,
	antialias:true,
	resolution:1,
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