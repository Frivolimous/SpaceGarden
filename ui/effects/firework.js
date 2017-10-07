var FIREWORK_GRAVITY=0.01;
var FIREWORK_FADE=0.01;
var FIREWORK_START_V_Y=1;
var FIREWORK_START_V_X=1;
var FIREWORK_NUM_PARTS=50;

var firework_particles=new Array();
var firework_initialized=false;

function firework_init(par){
	if (par!=null){
		if (par.gravity!=null) FIREWORK_GRAVITY=par.gravity;
		if (par.fade!=null) FIREWORK_FADE=par.fade;
		if (par.startVY!=null) FIREWORK_START_V_Y=par.startVY;
		if (par.startVX!=null) FIREWORK_START_V_X=par.startVX;
		if (par.numParts!=null) FIREWORK_NUM_PARTS=par.numParts;
	}
	app.ticker.add(firework_fade);
	firework_initialized=true;
}

function firework_constructor(par){
	if (!firework_initialized) firework_init();

	if (par==null) par={};
	

	if (par.x==null) par.x=0;
	if (par.y==null) par.y=0;
	if (par.color==null) par.color=0xffffff;
	if (par.gravity==null) par.gravity=FIREWORK_GRAVITY;
	if (par.fade==null) par.fade=FIREWORK_FADE;
	if (par.startVX==null) par.startVX=FIREWORK_START_V_X;
	if (par.startVY==null) par.startVY=FIREWORK_START_V_Y;
	if (par.numParts==null) par.numParts=FIREWORK_NUM_PARTS;
	if (par.addTo==null) par.addTo=app.stage;
	for (var i=0;i<par.numParts;i+=1){
		firework_particle(par);
	}
}

function firework_fade(){
	for (var i=0;i<firework_particles.length;i+=1){
		firework_particles[i].x+=firework_particles[i].vX;
		firework_particles[i].y+=firework_particles[i].vY;
		firework_particles[i].vY+=firework_particles[i].gravity;
		firework_particles[i].alpha-=firework_particles[i].fade;
		if (firework_particles[i].alpha<0.1){
			firework_particles[i].destroy();
			firework_particles.splice(i,1);
			i-=1;
		}
	}
}

function firework_particle(par){
	var m=new PIXI.Graphics();
	//m.beginFill(par.color);
	m.lineStyle(1,par.color);
	m.drawCircle(0,0,1+Math.random()*2);
	m.x=par.x;
	m.y=par.y;
	m.gravity=par.gravity;
	m.fade=par.fade;
	m.vX=Math.random()*par.startVX-par.startVX/2;
	m.vY=0-Math.random()*par.startVY;
//	m.alpha=0.7+0.3*Math.random();
	firework_particles.push(m);
	par.addTo.addChild(m);
	return m;
}