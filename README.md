# VamToolbox
The idea behind this program is to have empty ```VAM``` directory, terrabytes of vars in ```REPO``` directory and a defined subset of vars that you want to play with.  
There are two ways to make it work.   

The first way is to manually copy (or do a hardlink) some vars/scenes to AddonPackages/Scenes directoriers and then using "Search for missing dependencies" button.
It will lookup up what files are need to correctly load files you've copied and then soft-link them from ```REPO``` to ```VAM```.

Second way (more roboust) is to define various profiles.  
For example you can create profile called "Fav looks" and define it as list of files: ```Niko3dx.Someone.1.var```, ```A1X.Person.2.var``` etc  
Then you can define profile called ```Kitty mocaps``` and define it as all vars inside ```REPO/KittyMocaps``` directory.  
You can create as many profiles as you wan (they will be saved in VamToolbox settings).  
Then in UI just select profiles you want to soft-link and hit ```Apply profiles``` button. 

## Technical info
To make the soft-links working you have to run VamToolbox as admin.

VamToolbox will create local sqlite database to store some information between runs.

Program ignores meta.json file inside vars as they are often incorrect or superfluous. Instead it will read each individual json/preset file inside var to determine it's dependencies.

Program will read both: var files and assets from Custom directory.
If there is a Custom directory inside ```REPO``` dir then it will be read as well so you can just have Fat Vam installation and treat is as ```REPO``` and then have a clean VAM installation where stuff will be soft-linked.

## Operations

1. ![image](https://user-images.githubusercontent.com/59397941/156947078-065fd8a4-4402-4190-99af-74b5904b37fb.png)  
Locate missing dependencies in your ```VAM``` directory and soft-link/move them from ```REPO``` They will be soft-linked to ```VAM/AddonPackages/other``` directory.

2. ![image](https://user-images.githubusercontent.com/59397941/171236235-b727921c-6872-4c95-ae4f-1bc6dccc45fe.png)  
Trust scripts from all vars that are located in ```VAM```.

3. ![image](https://user-images.githubusercontent.com/59397941/171235432-a1dc8fed-a2a8-4102-8dd4-ef1dc54f0ee0.png)  
Scan ```VAM``` and ```REPO``` directories for errors/missing vars or assets. Check logs in exe directory for details.  
You can fix some of them manually like invalid var filename or missing meta.json

4. ![image](https://user-images.githubusercontent.com/59397941/156947430-d014f2a5-e478-4499-978a-8f8e323dd098.png)  
This will check what vars are missing in your ```VAM``` directory and download them from HUB. They will be downloaded to ```VAM/AddonPackages/other``` directory.

5. ![image](https://user-images.githubusercontent.com/59397941/171235499-36a0a33c-5a48-491e-9dab-8a84e153f586.png)  
First button will clear the "dependencies" section in every meta.json. It's often incorrect and the only purpose it has is to spam with errors in VaM.  
Warning! This make take over an hour depending on how many vars you have. Unfortunately updating one file in .var requires to recreate the archive (this is how zip works).  
Second button will remove preloadMorphs: true from every meta.json that it has. It will do it only for vars that are not morphpacks (i.e. vars that only contain morphs).  
Third button will do both things at once. Usefull since both operations have to recreate zip archive which can be slow.  
Both operations will backup your meta.json inside the var file so you can restore them if need by using "Restore meta.json" button. 
It is recommended to run them first with "Dry run" and check the relevant log file to see what has been changed.

## Profiles
![image](https://user-images.githubusercontent.com/59397941/156947461-51a9093d-c82c-4a95-8b6b-793a8c347fde.png)  
Applying a profile(s) will soft-link all matched vars to your ```VAM``` (preserving folder hierarchy).
Then dependencies will be resolved and soft-linked to ```VAM/AddonPackages/other``` directory.

```Manage profiles``` button will open a new window where you can create new profiles.  
Each profile can be either a directory with var files or a single var file.

## Options
1. ![image](https://user-images.githubusercontent.com/59397941/156947034-2f3c83d3-7b33-4631-9ebb-c2320b506c07.png)  
Thicking this will make the program execute all the logic but will not touch your files.

2. ![image](https://user-images.githubusercontent.com/59397941/156947049-93372224-c50e-4ece-80ad-297bcc8c73b0.png)  
Sometimes you want to move files from ```REPO``` to your ```VAM``` instead of doing soft-links. This only applies for "Search for missing dependencies" button.

3. ![image](https://user-images.githubusercontent.com/59397941/156947065-480d864b-0520-44e8-819e-7becb6aeb4a4.png)  
Ticking this will remove all soft-links in your ```VAM/Custom``` and ```VAM/AddonPackages``` directories when applying a profile or using "Search for missing dependencies" button.
