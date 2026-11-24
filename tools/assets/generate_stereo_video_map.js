// BIOFIT DASH: deterministic video-reference Stereo Madness map generator.
// Run: node tools/assets/generate_stereo_video_map.js
// Run: node tools/assets/generate_stereo_video_map.js --check
// IMPORTANT: Video-guided adaptation, NOT an exact original level object export.
const fs = require("fs");
const path = require("path");
const buildMap = function buildMap(){
 const map=[],seen=new Set();
 const add=(type,x,y,rotation=0)=>{x=Math.round(x);y=Math.round(y);const key=[type,x,y,rotation].join(":");if(seen.has(key))return;seen.add(key);const obj={type,x,y};if(rotation)obj.rotation=rotation;map.push(obj);};
 const tiles=(x,w,y,h=1)=>{for(let i=0;i<w;i++)for(let j=0;j<h;j++)add("tile",x+i,y+j);};
 const floatline=(x,w,y)=>{for(let i=0;i<w;i++)add("smallTile",x+i,y);};
 const groundspikes=(xs)=>{for(const x of xs)add("spike",x,0)};
 const ceiling=(xs)=>{for(const x of xs)add("spike",x,7,180)};
 const stars=(xs,y)=>{for(const x of xs)add("biofitDecoStar",x,y);};
 // Visual guide: user's 95.52-second 1280x720/60fps video.
 // 1.5x * 6.5 u/s makes 825 u ~= 85.1s of active run.
 // This is an approximate, playable reconstruction, NOT original serialized
 // RobTop object data. Replace only after original level coordinates verified.
 groundspikes([16,37,39,59,60,62]);
 tiles(49,4,0);tiles(63,4,0,2);tiles(81,3,0,2);
 groundspikes([50,74,76,84,86]);
 tiles(97,17,0);add("spike",105,1);groundspikes([119,121]);
 tiles(130,12,0);tiles(137,7,1);groundspikes([145,146,147,149]);
 floatline(158,3,2);floatline(170,4,3);groundspikes([154,156,175,177,179]);
 tiles(183,13,0,2);groundspikes([198,200]);floatline(202,4,3);
 tiles(210,12,0);tiles(214,8,1);groundspikes([221,223]);
 floatline(229,3,3);groundspikes([232,234,236]);
 stars([27,87,143,198,227],4);
 // Phase transition first ship; geometry based on the 30-45% video section.
 add("biofitShipPortal",245,3);
 ceiling([256,258,260,280,282,284,318,320,343,345,347,365,367]);
 groundspikes([265,266,290,292,294,313,315,335,337,354,356]);
 tiles(271,2,0,2);tiles(303,2,0,3);tiles(329,2,0,2);tiles(360,2,0,3);
 floatline(270,3,6);floatline(346,3,6);
 add("biofitStereoCoin",285,4);
 stars([250,298,356],5);
 add("biofitCubePortal",381,3);
 // Cube part: staggered blocks, rhythmic spikes and platforms.
 tiles(388,17,0);groundspikes([407,409,412]);
 tiles(416,2,0,3);tiles(428,5,0);groundspikes([434,436,438]);
 tiles(444,2,0,2);tiles(455,2,0,4);groundspikes([448,450,460,462,465]);
 tiles(471,2,0,5);floatline(482,4,3);floatline(493,4,4);
 groundspikes([476,478,480,485,487,489]);
 floatline(502,5,3);floatline(514,4,2);floatline(528,4,3);
 groundspikes([503,505,507,509,511,513,515]);
 tiles(537,4,0);groundspikes([542,544,546]);
 floatline(550,4,2);groundspikes([556,558]);
 tiles(567,3,0);groundspikes([572,574,576]);
 tiles(588,4,0,2);groundspikes([581,583,584,600,602]);
 floatline(607,5,2);groundspikes([611,613,614,616,617]);
 tiles(623,3,0,2);groundspikes([626,628]);
 floatline(634,4,2);tiles(643,2,0,2);groundspikes([645,647,650]);
 tiles(656,2,0,3);groundspikes([658,660,662,664]);
 floatline(669,4,3);groundspikes([670,672]);
 tiles(681,3,0);groundspikes([686,688,690]);
 floatline(695,4,3);groundspikes([697,699,701]);
 tiles(707,2,0,2);groundspikes([711,713,715,717]);
 add("biofitStereoCoin",620,4);
 stars([404,459,509,561,612,671,707],5);
 // Final ship sequence: tighter corridor and staggered floor/roof spikes.
 add("biofitShipPortal",724,3);
 tiles(732,4,0,2);tiles(743,4,0,3);tiles(761,3,0,3);
 groundspikes([737,739,754,756,767,769,779,780]);
 ceiling([733,735,738,749,751,759,761,764,773,775,777]);
 floatline(742,5,6);
 add("biofitStereoCoin",780,5);
 add("biofitCubePortal",786,3);
 // End sequence: floor/cube gap, few challenging yet short obstacles.
 tiles(792,9,0);groundspikes([800,802,804]);floatline(805,4,2);
 groundspikes([809,811]);tiles(815,4,0);groundspikes([820,822]);
 stars([795,811],4);
 add("ground",825,-2);
 map.sort((a,b)=>a.x-b.x||a.y-b.y||a.type.localeCompare(b.type));
 return {id:1,name:"Nivel 1",difficulty:1,map,music:"StereoMadness"};
};
const target = path.join(__dirname, "..", "..", "Assets", "BIOFIT", "Resources", "BiofitThreeModes", "BackOnTrack.json");
const content = JSON.stringify(buildMap(), null, 2) + "\n";
if (process.argv.includes("--check")) {
  if (!fs.existsSync(target) || fs.readFileSync(target, "utf8") !== content) {
    console.error("Stereo Madness Level 1 JSON differs from its generator"); process.exit(1);
  }
  console.log("PASS Level 1 JSON deterministic video-map");
} else {
  fs.writeFileSync(target, content, "utf8");
  console.log("Generated", target);
}
