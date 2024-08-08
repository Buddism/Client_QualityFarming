//#execOnChange
//buddy development stuff
if(isFunction("execChangedAddFile")) execChangedAddFile(ExpandFilename("./client.cs"));

if($Pref::QualityFarming::SavePath $= "")
	$Pref::QualityFarming::SavePath = "config/client/QualityFarming.cs";

if(!$QualityFarming::Loaded)
{
	if(isFile($Pref::QualityFarming::SavePath))
	{
		exec($Pref::QualityFarming::SavePath);
	} else {
		$QualityFarming::DateCreated = getDateTime();
	}

	$QualityFarming::Loaded = true;
	QualityFarming_save();
}

package QualityFarming
{
	function NMH_Type::send(%this)
	{
		%message = %this.getValue();
		if(getSubStr(%message, 0, 1) !$= "/")
			return parent::send(%this);
		
		// /qf and /fb cuz !fb is used habitually
		%commandArgs = restWords(%message);
		switch$(firstWord(%message))
		{
			case "/qfHelp" or "/fbHelp":
				newChatHud_AddLine("\c6Commands: ToolID is the number that looks like this [de7]");
				newChatHud_AddLine("\c3  /qfToggle");
				newChatHud_AddLine("\c3  /qfName \c7[\c6ID or 'last'\c7] \c7[\c6Name\c7]");
				newChatHud_AddLine("\c3  /qfNote \c7[\c6ID or 'last'\c7] \c7[\Note\c7] \c6- adds a note to the item to show in /qfList");
				newChatHud_AddLine("\c3  /qfLast \c6- last seen tool id");
				newChatHud_AddLine("\c3  /qfList \c7[\c61 to show unnamed\c7]");
				newChatHud_AddLine("\c3  \c0/qfDelete \c7[\c6ID\c7] \c6- delete all tool info for this id");

			case "/qfLast" or "/fbLast":
				%lastSeenID = $QualityFarming::lastSeenToolID $= "" ? "Unkown" : $QualityFarming::lastSeenToolID;
				newChatHud_AddLine("\c6QualityFarming: Last seen tool id is [<spush><font:Consolas:" @ QualityFarming_getCorrectFontSize() @ ">\c6"@ %lastSeenID @"<spop>\c6]");

			case "/qfToggle" or "/fbToggle":
				$QualityFarming::Enabled = !$QualityFarming::Enabled;
				newChatHud_AddLine("\c6QualityFarming is now" SPC ($QualityFarming::Enabled ? "\c3Enabled" : "\c0Disabled"));
				%doFarmingSave = true;

			case "/qfList" or "/fbList":
				QualityFarming_listBoundIDs(%commandArgs);

			case "/qfName" or "/fbName":
				QualityFarming_setIDName(%commandArgs);
				%doFarmingSave = true;

			case "/qfNote" or "/fbNote":
				QualityFarming_setIDNote(%commandArgs);
				%doFarmingSave = true;

			case "/qfDelete" or "/fbDelete":
				%didDeletion = QualityFarming_DeleteToolID(%commandArgs);
				if(%didDeletion)
				{
					newChatHud_AddLine("\c6QualityFarming: \c6Deleted tool tata!");
					%doFarmingSave = true;
				} else {
					newChatHud_AddLine("\c6QualityFarming: \c0Failed to find tool id!");
				}
		}

		QualityFarming_save();
		return parent::send(%this);
	}

	function clientCmdCenterprint(%message, %time)
	{
		if(!$QualityFarming::Enabled)
			return parent::clientCmdCenterprint(%message, %time);

		%lines = getRecordCount(%message);
		for(%i = 0; %i < %lines; %i++)
		{
			%line = getRecord(%message, %i);
			%line = QualityFarming_parseToolIDString(%line);

			%newMessage = %i == 0 ? %line : %newMessage NL %line;
		}
		
		parent::clientCmdCenterprint(%newMessage, %time);
	}

	function clientCmdMessageBoxOK (%title, %message)
	{
		if($QualityFarming::Enabled
			&& $QualityFarming::PreventStorageFullBox
			&& deTag(%title)   $= "Storage Full"
			&& deTag(%message) $= "Cannot insert this item!")
			return;

		return parent::clientCmdMessageBoxOK (%title, %message);
	}
};
activatePackage(QualityFarming);

function QualityFarming_parseToolIDString(%line)
{
	//check if there is an item id here
	%pos0 = stripos(%line, "[");
	%pos1 = stripos(%line, "]");
	if(%pos0 == -1 || %pos1 == -1 || %pos1 - %pos0 != 4)
		return %line;
	
	%toolID = getSubStr(%line, %pos0 + 1, 3);
	if(!QualityFarming_veryfiyID(%toolID))
		return %line;
	
	%name = QualityFarming_getToolName(%toolID);
	$QualityFarming::lastSeenToolID = %toolID;
	if(%name !$= "")
	{
		%newLine = getSubStr(%line, 0, %pos0 + 1) @ %name @ getSubStr(%line, %pos1, 256);
		return %newLine;
	}

	return %line;
}

function QualityFarming_veryfiyID(%id)
{
	if(strlen(%id) != 3)
		return false;

	if(strlen(stripChars(strlwr(%id), "abcdefghijklmnopqrstuvwxyz" @ "0123456789")) != 0)
		return false;
	
	return true;
}

function QualityFarming_getToolName(%toolID)
{
	return $QualityFarming::ToolData[%toolID, "name"];
}

function QualityFarming_setIDName(%data)
{
	%toolID = getWord(%data, 0);
	if(%toolID $= "last")
	{
		%toolID = $QualityFarming::lastSeenToolID;
	}

	%name = collapseEscape(restWords(%data));
	if(!QualityFarming_veryfiyID(%toolID))
	{
		newChatHud_AddLine("\c6QualityFarming: Error id is invalid");
		return;
	}
	
	%index = QualityFarming_getToolIndex(%toolID);
	if(%index == -1)
		QualityFarming_addToolIndex(%toolID);

	$QualityFarming::ToolData[%toolID, "name"] = %name;
	newChatHud_AddLine("\c6QualityFarming: Named ["@ %toolID @"] to '"@ %name @"'");
}

function QualityFarming_setIDNote(%data)
{
	%toolID = getWord(%data, 0);
	if(%toolID $= "last")
	{
		%toolID = $QualityFarming::lastSeenToolID;
	}

	%note = collapseEscape(restWords(%data));
	if(!QualityFarming_veryfiyID(%toolID))
	{
		newChatHud_AddLine("\c6QualityFarming: Error id is invalid");
		return;
	}
	
	%index = QualityFarming_getToolIndex(%toolID);
	if(%index == -1)
		QualityFarming_addToolIndex(%toolID);

	$QualityFarming::ToolData[%toolID, "note"] = %note;
	newChatHud_AddLine("\c6QualityFarming: Noted ["@ %toolID @"] with '"@ %note @"'");
}

function QualityFarming_getToolIndex(%testToolID)
{
	for(%i = 0; %i < $QualityFarming::ToolDataCount; %i++)
	{
		%toolID = $QualityFarming::ToolDataIndex[%i];
		if(%testToolID $= %toolID)
			return %i;
	}

	return -1;
}

function QualityFarming_addToolIndex(%toolID)
{
	if(QualityFarming_getToolIndex(%toolID) != -1)
		return false;
	
	$QualityFarming::ToolDataExists[%toolID] = true;

	$QualityFarming::ToolDataIndex[$QualityFarming::ToolDataCount + 0] = %toolID;
	$QualityFarming::ToolDataCount++;	
}

function QualityFarming_deleteToolID(%toolID)
{
	%toolIndex = QualityFarming_getToolIndex(%toolID);
	if(%toolIndex == -1)
		return false;

	for(%i = %toolIndex; %i < $QualityFarming::ToolDataCount; %i++)
	{
		$QualityFarming::ToolDataIndex[%i] = $QualityFarming::ToolDataIndex[%i + 1];
	}

	$QualityFarming::ToolDataCount--;
	$QualityFarming::ToolDataIndex[$QualityFarming::ToolDataCount] = "";

	deleteVariables("$QualityFarming::ToolData" @ %toolID @"_*");
	return true;
}

function QualityFarming_listBoundIDs(%showUnnamed)
{
	%showUnnamed = %showUnnamed !$= "";
	newChatHud_AddLine("\c6Total Tool Data Records #" @ $QualityFarming::ToolDataCount + 0);
	for(%i = 0; %i < $QualityFarming::ToolDataCount; %i++)
	{
		%toolID = $QualityFarming::ToolDataIndex[%i];
		%name = $QualityFarming::ToolData[%toolID, "name"];
		if(!%showUnnamed && %name $= "")
			continue;
		
		if(%name $= "")
			%name = "<color:CCCCCC>[UNNAMED]";

		%note = $QualityFarming::ToolData[%toolID, "Note"];
		newChatHud_AddLine("  <tab:200><spush><font:Consolas:" @ QualityFarming_getCorrectFontSize() @ ">\c3["@ %toolID @"]<spop>\c6 "@ %name @ "\t<color:999999>" SPC %note);
	}
}

function QualityFarming_save()
{
	$QualityFarming::lastSaveTime = getDateTime();
	export("$QualityFarming::*", $Pref::QualityFarming::SavePath);
}


function QualityFarming_getCorrectFontSize()
{
	//kinda dumb
	return NMH_Type.profile.fontSize;
}