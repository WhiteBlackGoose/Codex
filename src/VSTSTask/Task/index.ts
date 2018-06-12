import tl = require('vsts-task-lib/task');
import trm = require('vsts-task-lib/toolrunner');
import mod = require('./taskmod');
import http = require('https');
import fs = require('fs');
import request = require('request');

async function run() {
    try {
        let toolPath = "C:/temp/Codex.Automation.Workflow.exe";
        let tool: trm.ToolRunner;
        
        //download file
        var file = fs.createWriteStream(toolPath);

        await new Promise(resolve => request.get('https://github.com/Ref12/Codex/releases/download/latest-prerel/Codex.Automation.Workflow.exe').pipe(file).on('finish', resolve));
        tool = tl.tool(toolPath);
        let rc1: number = await tool.exec();

        console.log('Task done! ' + rc1);
    }
    catch (err) {
        tl.setResult(tl.TaskResult.Failed, err.message);
    }
}

run();