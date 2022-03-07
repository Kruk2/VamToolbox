# VamRepacker
The idea behind this program is to have empty ```VAM``` directory, terrabytes of vars in ```REPO``` directory and a defined subset of vars that you want to play with.  
There are two ways to make it work.   

The first way is to manually copy some vars/scenes to AddonPackages/Scenes directoriers and then using "Search for missing dependencies" button.
It will lookup up what files are need to correctly load files you've copied and then soft-link them from ```REPO``` to ```VAM```.

Second way (more roboust) is to define various profiles.  
For example you can create profile called "Fav looks" and define it as list of files: ```Niko3dx.Someone.1.var```, ```A1X.Person.2.var``` etc
Then you can define profile called ```Kitty mocaps``` and define it as all vars inside ```REPO/KittyMocaps``` directory.
You can create as many profiles as you wan (they will be saved in VamRepacker settings).
Then in UI just select profiles you want to soft-link and hit ```Apply profiles``` button.

## technical info
To make the soft-links working you have to run VamRepacker as admin.

VamRepacker will create local sqlite database to store some information between runs.

Program ignores meta.json file inside vars as they are often incorrect. Instead it will read each individual json/preset file inside var to determine it's dependencies.

## Operations

1. ![image](https://user-images.githubusercontent.com/59397941/156947078-065fd8a4-4402-4190-99af-74b5904b37fb.png)  
Locate missing dependencies in your ```VAM``` directory and soft-link/move them from ```REPO``` They will be soft-linked to ```VAM/AddonPackages/other``` directory.

2. ![image](https://user-images.githubusercontent.com/59397941/156947246-63d7f929-044c-467c-bdb6-6027194f291e.png)  
Trust scripts from all vars that are located in ```VAM```.

3. ![image](https://user-images.githubusercontent.com/59397941/156947268-30999225-3080-4696-a7e1-79b397b784bd.png)  
Scan ```VAM``` and ```REPO``` directories for errors/missing vars or assets. Check logs in exe directory for details.  
You can fix some of them manually like invalid var filename or missing meta.json

4. ![image](https://user-images.githubusercontent.com/59397941/156947430-d014f2a5-e478-4499-978a-8f8e323dd098.png)
This will check what vars are missing in your ```VAM``` directory and download them from HUB. They will be downloaded to ```VAM/AddonPackages/other``` directory.

## Profiles
![image](https://user-images.githubusercontent.com/59397941/156947461-51a9093d-c82c-4a95-8b6b-793a8c347fde.png) 
Applying a profile(s) will soft-link all matched vars to your ```VAM``` (preserving folder hierarchy).
Then dependencies will be resolved and soft-linked to ```VAM/AddonPackages/other``` directory.

```Manage profiles``` button will open a new window where you can create new profiles.  
Each profile can be either a directory with var files or a single var file.

## Options
1. ![image](https://user-images.githubusercontent.com/59397941/156946841-62b7cac8-61eb-4c5e-966d-c8dfc7eb347f.png)  
This will use shallow dependency resolver to limit the number of vars/files being soft-linked/copied.  
For example let's say that your ```scene.var``` wants a look from ```another.var```. That Look in ```another.var``` references some png files from ```textures.var```.  
But ```another.var``` has 20 other looks that are using 50 other packages that you don't really need for your ```scene.var``` to load!  
Deep dependency resolver (checkbox unticked) will soft-link ```scene.var```, ```another.var```, ```textures.var``` and all dependencies used by ```another.var```.  
Shallow dependency resolver (checkbox ticked) will soft-link only ```scene.var```, ```another.var``` and ```textures.var```.  

2. ![image](https://user-images.githubusercontent.com/59397941/156947034-2f3c83d3-7b33-4631-9ebb-c2320b506c07.png)  
Thicking this will make the program execute all the logic but will not touch your files.

3. ![image](https://user-images.githubusercontent.com/59397941/156947049-93372224-c50e-4ece-80ad-297bcc8c73b0.png)  
Sometimes you want to move files from ```REPO``` to your ```VAM``` instead of doing soft-links. This only applies for "Search for missing dependencies" button.

4. ![image](https://user-images.githubusercontent.com/59397941/156947065-480d864b-0520-44e8-819e-7becb6aeb4a4.png)  
Ticking this will remove all soft-links in your ```VAM``` directory when applying a profile or using "Search for missing dependencies" button.
