// Hand-drawn stand-in geometry, lifted coordinate for coordinate from
// design/canvas4/Main.dc.html. It is a drawing of what a district map looks
// like, not data: no shape here corresponds to a real place.
//
// It is only the decoration behind the home card now. The result map draws real
// boundaries, and nothing from this file reaches it.

export const DISTRICT_POLYGONS = [
  '0,0 320,-10 350,215 0,240',
  '320,-10 640,10 610,270 350,215',
  '640,10 960,-5 985,235 610,270',
  '960,-5 1280,5 1250,265 985,235',
  '1280,5 1600,0 1600,245 1250,265',
  '0,240 350,215 300,485 0,510',
  '350,215 610,270 665,530 300,485',
  '610,270 985,235 930,495 665,530',
  '985,235 1250,265 1310,520 930,495',
  '1250,265 1600,245 1600,490 1310,520',
  '0,510 300,485 365,770 0,745',
  '300,485 665,530 620,725 365,770',
  '665,530 930,495 1000,765 620,725',
  '930,495 1310,520 1270,735 1000,765',
  '1310,520 1600,490 1600,760 1270,735',
  '0,745 365,770 320,1000 0,1000',
  '365,770 620,725 640,1000 320,1000',
  '620,725 1000,765 960,1000 640,1000',
  '1000,765 1270,735 1280,1000 960,1000',
  '1270,735 1600,760 1600,1000 1280,1000',
]

export const RIVER_PATH =
  'M-20,300 C260,340 380,470 620,500 C840,528 980,470 1180,520 ' +
  'C1360,564 1480,640 1620,660 L1620,724 C1460,704 1340,632 1170,588 ' +
  'C980,540 850,598 615,566 C360,534 240,404 -20,364 Z'

export const ROAD_PATHS = [
  'M-20,640 L420,600 L760,700 L1180,650 L1620,690',
  'M520,-20 L560,300 L500,640 L560,1020',
  'M1080,-20 L1040,260 L1120,540 L1080,1020',
]

/** The district the address falls in, as the wide home board draws it. */
export const WIDE_FOCUS = {
  polygon: '930,495 1310,520 1270,735 1000,765',
  hatching:
    'M950,730 L1290,505 M985,762 L1305,540 M938,660 L1240,462 M1000,780 L1312,585',
  crosshair: 'M1127,571 V687 M1069,629 H1185',
  center: { x: 1127, y: 629 },
  pinRadius: 8.5,
  ringRadius: 25,
}

/**
 * The phone board slices a narrower window out of the same drawing, so it
 * highlights the district that sits in the middle of that window instead.
 */
export const NARROW_FOCUS = {
  polygon: '610,270 985,235 930,495 665,530',
  hatching: 'M640,470 L960,262 M672,510 L975,300 M628,400 L940,240',
  crosshair: 'M797,312 V452 M727,382 H867',
  center: { x: 797, y: 382 },
  pinRadius: 11,
  ringRadius: 30,
}

export const WIDE_TICKS = {
  top: [
    { value: '800', x: 806 },
    { value: '1000', x: 1006 },
    { value: '1200', x: 1206 },
    { value: '1400', x: 1406 },
  ],
  right: [
    { value: '200', y: 206 },
    { value: '400', y: 406 },
    { value: '600', y: 606 },
    { value: '800', y: 806 },
  ],
}

export const NARROW_TICKS = [
  { value: '600', x: 600 },
  { value: '800', x: 800 },
  { value: '1000', x: 1000 },
]

/** The whole survey sheet, behind the home card. */
export const OVERVIEW = {
  viewBox: '0 0 1600 1000',
  ground: { x: 0, y: 0, width: 1600, height: 1000 },
}
