const inputM={
	mouseObjects:new Array,
	defaultMode:0,
	dragFunction:null,
	gameCanvas:{x:0,y:0},
	TIME_DIFFERENCE:1000,
}

var mouse={x:0,y:0,down:false};
var keyCodes={left:"ArrowLeft",right:"ArrowRight",up:"ArrowUp",down:"ArrowDown"};
var keyStates={left:false,right:false,up:false,down:false};
var temp_type=0;
var temp_forcedColor=0x012000;
var temp_forcedShape=0;
var temp_forcedSize=2;

function input_init(){
	window.addEventListener("keydown",onKeyDown)
	window.addEventListener("keyup",onKeyUp)
	window.addEventListener("pointerdown",onMouseDown);
	window.addEventListener("pointerup",onMouseUp);
	window.addEventListener("pointermove",onMouseMove);
	EventManager.addEventListener(EventManagerTypes.UI_SELECT_MODE,onUISelect);
}

function onMouseDown(e){
	let _mouseObject=input_findMouseObject(e.pointerId);
	if (_mouseObject==null){
		/*inputM.mouseObjects.push(new input_MouseObject({
			x:(e.x-stageBorders.left)/gameM.scale-inputM.gameCanvas.x/gameM.scale,
			y:(e.y-stageBorders.top)/gameM.scale-inputM.gameCanvas.y/gameM.scale,
			down:true,
			id:100,
			mode:inputM.defaultMode,
			data:e.data
		}));*/
		_mouseObject=new input_MouseObject({
			stageX:e.x-stageBorders.left,
			stageY:e.y-stageBorders.top,
			x:(e.x-stageBorders.left)/gameM.scale-inputM.gameCanvas.x/gameM.scale,
			y:(e.y-stageBorders.top)/gameM.scale-inputM.gameCanvas.y/gameM.scale,
			down:true,
			id:e.pointerId,
			mode:inputM.defaultMode,
			data:e.data
		});
		for (var i=0;i<inputM.mouseObjects.length;i+=1){
			if (Math.abs(_mouseObject.time-inputM.mouseObjects[i].time)<inputM.TIME_DIFFERENCE){ 
				inputM.mouseObjects[i].gesturePair=_mouseObject;
				_mouseObject.gesturePair=inputM.mouseObjects[i];
				inputM.mouseObjects[i].drag=null;
				gameM.forces.removePull(inputM.mouseObjects[i]);

				let dx=inputM.mouseObjects[i].x-(e.x-stageBorders.left);
				let dy=inputM.mouseObjects[i].y-(e.y-stageBorders.top);
				_mouseObject.gestureDist=Math.sqrt(dx*dx+dy*dy);// / gameM.scale;
				_mouseObject.gestureStartZoom=gameM.scale;
				inputM.mouseObjects[i].gestureDist=_mouseObject.gestureDist;
				inputM.mouseObjects[i].gestureStartZoom=_mouseObject.gestureStartZoom;
			}
		}
		
		if (inputM.dragFunction!=null && _mouseObject.gesturePair==null){
			e.gameX=_mouseObject.x;
			e.gameY=_mouseObject.y;
			let _drag=inputM.dragFunction(e);
			if (_drag!=null){
				_mouseObject.drag=_drag;
				gameM.forces.addPull(_mouseObject);
				//_drag.active=false; //NOT GENERAL
				//_drag.target=_mouseObject;
				//_mouseObject.dragOffX=-_drag.radius;
				//_mouseObject.dragOffY=-_drag.radius;
				_mouseObject.x+=_mouseObject.dragOffX/gameM.scale;
				_mouseObject.y+=_mouseObject.dragOffY/gameM.scale;
			}
		}

		inputM.mouseObjects.push(_mouseObject);
	}else{
		_mouseObject.down=true;
	}
}

function onMouseUp(e){
	for (var i=0;i<inputM.mouseObjects.length;i+=1){
		if (e.pointerId==inputM.mouseObjects[i].id){
			if (inputM.mouseObjects[i].mode==0){
				if (inputM.mouseObjects[i].drag!=null){
					//inputM.mouseObjects[i].drag.active=true; //NOT GENERAL
					//inputM.mouseObjects[i].drag.target=null;
					gameM.forces.removePull(inputM.mouseObjects[i]);
				}
				inputM.mouseObjects.splice(i,1);
			}

			return;
		}
	}
}

function onMouseMove(e){
	let _mouseObject=input_findMouseObject(e.pointerId);
	if (_mouseObject!=null){
		_mouseObject.stageX=e.x-stageBorders.left;
		_mouseObject.stageY=e.y-stageBorders.top;
		_mouseObject.x=(e.x-stageBorders.left+_mouseObject.dragOffX-inputM.gameCanvas.x)/gameM.scale;
		_mouseObject.y=(e.y-stageBorders.top+_mouseObject.dragOffY-inputM.gameCanvas.y)/gameM.scale;
		if (_mouseObject.gesturePair!=null){
			let _dX=_mouseObject.gesturePair.x-_mouseObject.stageX;
			let _dY=_mouseObject.gesturePair.y-_mouseObject.stageY;
			//let _distance=Math.abs(Math.sqrt(_dX*_dX+_dY*_dY)-_mouseObject.gestureDist);
			let _distance0=Math.sqrt(_dX*_dX+_dY*_dY);
			let _distance=_distance0-_mouseObject.gestureDist;

			let _zoom= _mouseObject.gestureStartZoom/_mouseObject.gestureDist*_distance0;
			game_zoomTo(_zoom);
			//console.log(_distance0+" "+_mouseObject.gestureDist+" "+gameM.scale);

			/*if (Math.abs(_distance)>1){
				game_zoom(_distance*0.001);
			}*/
			//console.log(gameM.scale);
		}
	}
}

// zoom = dist/oldDist*CONST;

// 1 = 250 / 250 * 1 ;
// 2 = 500 / 250 * 1 ;

function input_findMouseObject(_id){
	for (var i=0;i<inputM.mouseObjects.length;i+=1){
		if (_id==inputM.mouseObjects[i].id){
			return inputM.mouseObjects[i];
		}
	}
	return null;
}

function input_MouseObject(par){
	par = par || {};
	this.x=par.x || 0;
	this.y=par.y || 0;
	this.stageX=par.stageX || 0;
	this.stageY=par.stageY || 0;
	this.down=par.down || false;
	this.drag=par.drag || null;
	this.dragOffX=par.dragOffX || 0;
	this.dragOffY=par.dragOffY || 0;
	this.id=par.id || 0;
	this.mode=par.mode || 0;
	this.data=par.data || {};
	this.time=new Date;
	this.gesturePair=null;
	this.gestureDist=0;
	this.gestureStartZoom=0;
}

function onKeyDown(e){
	switch(e.key){
		case keyCodes.left: keyStates.left=true; break;
		case keyCodes.right: keyStates.right=true; break;
		case keyCodes.down: keyStates.down=true; break;
		case keyCodes.up: keyStates.up=true; break;
		/*case "0": ui_selectButtonAt(0); break;//temp_type=0; break;
		case "1": temp_type=1; break;
		case "2": temp_type=2; break;
		case "3": temp_type=3; break;
		case "4": temp_type=4; break;
		case "5": temp_type=5; break;
		case "6": temp_type=6; break;
		case "7": temp_type=7; break;
		case "8": temp_type=8; break;
		case "9": temp_type=9; break;*/

		case "1": ui_selectButtonAt(0); break;
		case "2": ui_selectButtonAt(1); break;
		case "3": ui_selectButtonAt(2); break;
		case "4": ui_selectButtonAt(3); break;
		case "5": ui_selectButtonAt(4); break;
		case "6": ui_selectButtonAt(5); break;
		case "7": ui_selectButtonAt(6); break;
		case "8": ui_selectButtonAt(7); break;
		case "9": ui_selectButtonAt(8); break;
		case "0": ui_selectButtonAt(9); break;

		case " ": running=!running; break;
		case "z": game_addRandomCrawler(); break;
		case "=": case "+": game_zoom(1.2); break;
		case "-": case "_": game_zoom(1/1.2); break;
		case "]": gameM.gameRate+=1; break;
		case "[": gameM.gameRate=Math.max(gameM.gameRate-1,1); break;
	}
}

function onKeyUp(e){
	switch(e.key){
		case keyCodes.left: keyStates.left=false; break;
		case keyCodes.right: keyStates.right=false; break;
		case keyCodes.up: keyStates.up=false; break;
		case keyCodes.down: keyStates.down=false; break;
		//case "p": gameM.gameRate=1; break;
	}
}

function onUISelect(e){
	temp_type=e.mode;
}