# ScummEditor

ScummEditor is a tool that can be used to export and import back graphics on Scumm v5 and v6 games. It can also be used to see the parsed data structure on these games (not all block types are parsed at the moment).

It was created by Matheus Moreira and was originally developed for internal use at ScummBR translation group (http://www.scummbr.com), to allow our artists to translate the game images that contains english texts.

This tool supports the extraction and injection of the following images:

 * Background Images (all codecs are supported, both for exporting and importing back)

 * Background Z-Planes

 * Objects Images - including multiple images per object.

 * Objects Z-Planes - including multiples z-planes per image.

 * Costumes - All frames.

As far as I know this is the only existing tool that allows you change the costumes on scumm games.

## Thanks
I would like to say thank you to Jestar Jokin (http://www.jestarjokin.net/blog/category/scumm/) for his great help and for being so nice with me, providing his source codes and pointing me to links where I could learn a lot about the scumm data and image formats. Without his help, this program never existed.

I also would like to say thanks to SCUMMVM (http://wiki.scummvm.org/index.php/SCUMM/Technical_Reference), SCUMM REVISITED (http://goblin.cx/scumm/scummrev/articles/image.html -- offline) and SCUMMC (https://github.com/jamesu/scummc/wiki) sites for providing a lot of useful technical information on their sites. I made this tool based on what I learned there.

This program utilizes BE.HexEditor to display binary data. More information and download of the control on http://sourceforge.net/projects/hexbox/
