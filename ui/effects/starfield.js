function starfield_construct(par){
	var _width=550;
	var _height=550;
	if (par.width!=null) _width=par.width;
	if (par.height!=null) _height=par.height;
	
	//var m=new PIXI.Sprite();
	var m=new PIXI.Graphics();

	m.tick=function(_distance){starfield_tick(m,_distance)};
	m.stars=new Array();
	
	m.starWidth=_width;
	m.starHeight=_height;
	m.starFrequency=2;
	m.starTick=0;
	return m;
}



function starfield_tick(_field,_distance){
	_field.clear();
	_field.beginFill(0xffffff);
	for (var i=0;i<_field.stars.length;i+=1){
		_field.stars[i].y+=_field.stars[i].speed*_distance;
		if (_field.stars[i].y>_field.starHeight){
			//_field.stars[i].destroy();
			_field.stars.splice(i,1);
			i-=1;
		}else{
			_field.drawRect(_field.stars[i].x,_field.stars[i].y,_field.stars[i].size,_field.stars[i].size);
		}
	}
	_field.starTick+=1;
	if (_field.starTick>_field.starFrequency){
		_field.starTick=0;
		//starfield_addStar(_field);
		starfield_addStarData(_field);
	}
}

function starfield_addStar(_field){
	var _speed=0.1+Math.random()*0.4
	var _star=starfield_star(Math.random()*_field.starWidth,0,_speed,_speed*2);
	_field.stars.push(_star);
	_field.addChild(_star);
}

function starfield_star(_x,_y,_speed,_size=1,_color=0xffffff){
	var m=new PIXI.Graphics();
	m.beginFill(_color);
	m.drawCircle(0,0,_size);
	m.x=_x;
	m.y=_y;
	m.speed=_speed;

	return m;
}

function starfield_addStarData(_field){
	var _speed=0.1+Math.random()*0.4
	var _star=starfield_starData(Math.random()*_field.starWidth,0,_speed,_speed*3);
	_field.stars.push(_star);
}

function starfield_starData(_x,_y,_speed,_size=1,_color=0xffffff){
	var m={
		x:_x,
		y:_y,
		speed:_speed,
		size:_size,
		color:_color
	}

	return m;
}

function starfield_drawStarTo(_field,_star){
	//_field.
}