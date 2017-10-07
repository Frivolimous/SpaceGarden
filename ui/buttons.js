function button_constructBasic(par){
	par=par || {};

	var m=uiElement_constructor({
		label:par.label,
		width:par.width||200,
		height:par.height||50,
		clickFunction:par.function,
		x:par.x||50,
		y:par.y||50,
		bgColor:par.bgColor||0x8080ff,
		alpha:par.alpha||1
	});
	m.buttonMode=true;
	return m;
}

function button_clearButton(_function){
	var m=uiElement_constructor({
		bgColor:0x00ff00,
		clickFunction:_function,
		alpha:0.05,
		width:190,
		height:50
	});
	m.buttonMode=true;
	return m;
}

function button_smallButton(_label,_function){
	_button=button_constructBasic({
		bgColor:0x113311,
		function:_function,
		width:40,
		height:40,
		label:_label
	});

	_button.label.y=10;
	_button.label.style.fontSize=14;
	_button.label.style.fill=0xf1f1f1;

	_button.setSelectState=function(bool){
		//console.log(bool);
		//console.log(this);
		if (bool){
			if (!this.selectRect){
				this.selectRect=new PIXI.Graphics();
				this.selectRect.lineStyle(2,0xffff00);
				this.selectRect.drawRect(0,0,this.graphics.width,this.graphics.height);
				this.addChild(this.selectRect);
			}
		}else{
			if (this.selectRect){
				this.selectRect.destroy();
				this.selectRect=null;
			}
		}
	}
	return _button;
}