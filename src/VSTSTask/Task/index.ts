import tl = require('vsts-task-lib/task');
import trm = require('vsts-task-lib/toolrunner');
import http = require('https');
import fs = require('fs');
import request = require('request');
import shell = require('shelljs');
import path = require('path');
import os = require('os');

function mkdir(directoryPath: string) {
    let cmdPath = tl.which('cmd');
    let tool = tl.tool(cmdPath).arg('/c').arg('mkdir ' + directoryPath);
    tool.execSync();
}

async function run() {
    try {
        let workflowArguments = tl.getDelimitedInput("WorkflowArguments", "\n", true);
        let outputDirectory = tl.getPathInput('CodexOutputRoot', true);        
        let codexBootstrapDirectory = path.join(outputDirectory, "bootstrap");
        let toolPath =path.join(codexBootstrapDirectory, "Codex.Automation.Workflow.exe");
        let tool: trm.ToolRunner;
        
        shell.mkdir("-p", codexBootstrapDirectory);

        //download file
        var file = fs.createWriteStream(toolPath);

        await new Promise(resolve => request.get('https://github.com/Ref12/Codex/releases/download/latest-prerel/Codex.Automation.Workflow.exe').pipe(file).on('finish', resolve));
        tool = tl.tool(toolPath).arg(workflowArguments).arg(`/codexOutputRoot:${outputDirectory}`);
        let rc1: number = await tool.exec();

        console.log('Task done! ' + rc1);
    }
    catch (err) {
        tl.setResult(tl.TaskResult.Failed, err.message);
    }
}

run();