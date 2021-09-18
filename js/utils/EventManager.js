
const EventManagerTypes={
	UI_SELECT_MODE:0,
	DRAG_EVENT:2,
	MOVE_EVENT:3,
}

const EventManager={
	events:[],
	activeEvents:[],

	registerRegistration:function(_type,_logEvents=false){
		EventManager.events[_type]=new EventManager_NewRegistration(_type);
		if (_logEvents) EventManager.addEventListener(_type,EventManager.logEvent);
	},
	addEventListener:function(_type,_function){
		EventManager.events[_type].listeners.push(_function);
	},
	removeEventListener:function(_type,_function){
		for (var i=0;i<EventManager.events[_type].listeners.length;i+=1){
			if (EventManager.events[_type].listeners[i]===_function){
				EventManager.events[_type].listeners.splice(i,1);
			}
		}
	},
	registerEvent:function(_type,_par){
		EventManager.events[_type].events.push(_par);
		if (!EventManager.events[_type].active){
			EventManager.activeEvents.push(EventManager.events[_type]);
			EventManager.events[_type].active=true;
		}
	},

	eventLog:[],
	logEvent:function(e){
		if (EventManager.eventLog.length>0){
			let _lastEvent=EventManager.eventLog[EventManager.eventLog.length-1];

			switch(e.eventType){
				case EventManagerTypes.MOVE_EVENT:
					if (_lastEvent.eventType===e.eventType && e.target.id===_lastEvent.target.id){
						EventManager.eventLog.pop();
					}
					break;
			}
		}
		
		EventManager.eventLog.push(e);
	},

	traceEventLog:function(){
		console.log(EventManager.eventLog);
	}
}

function EventManager_init(){
	app.ticker.add(EventManager_onTick);
	EventManager.registerRegistration(EventManagerTypes.UI_SELECT_MODE,true);
	EventManager.registerRegistration(EventManagerTypes.SHAPE_INTERACTION,true);
	EventManager.registerRegistration(EventManagerTypes.DRAG_EVENT,true);
	EventManager.registerRegistration(EventManagerTypes.MOVE_EVENT,true);
}

function EventManager_onTick(){
	while(EventManager.activeEvents.length>0){
		let _eventType=EventManager.activeEvents[0];

		while(_eventType.events.length>0){
			let _cEvent=_eventType.events[0];
			for (var i=0;i<_eventType.listeners.length;i+=1){
				_eventType.listeners[i](_cEvent);
			}
			_eventType.events.shift();
		}
		_eventType.active=false;
		EventManager.activeEvents.shift();
	}
}


function EventManager_NewRegistration(_type){
	this.type=_type;
	this.listeners=[];
	this.events=[];
	this.active=false;
}

function EventManager_UISelectModeEvent(_mode){
	this.eventType=EventManagerTypes.UI_SELECT_MODE;
	this.mode=_mode;
}

/*function EventManager_DragEvent(_drag,_onMove,_onEnd){
	this.eventType=EventManagerTypes.DRAG_EVENT;
	this.drag=_drag;
	this.startDrag=_startDrag;
	this.onMove=_onMove;
	this.onEnd=_onEnd;
}*/

function EventManager_DragEvent(_drag,_startDrag,_type){
	this.eventType=EventManagerTypes.DRAG_EVENT;
	this.drag=_drag;
	this.startDrag=_startDrag;
	this.type=_type;
}



function EventManager_MoveEvent(_target,_x,_y){
	this.eventType=EventManagerTypes.MOVE_EVENT;
	this.target=_target;
	this.x=_x;
	this.y=_y;
}

function EventManager_ShapeInteraction(_type,_x,_y,_target){
	this.eventType=EventManagerTypes.SHAPE_INTERACTION;

}