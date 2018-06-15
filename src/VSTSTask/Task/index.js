"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
const tl = require("vsts-task-lib/task");
const fs = require("fs");
const request = require("request");
const shell = require("shelljs");
const path = require("path");
function mkdir(directoryPath) {
    let cmdPath = tl.which('cmd');
    let tool = tl.tool(cmdPath).arg('/c').arg('mkdir ' + directoryPath);
    tool.execSync();
}
function run() {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            let workflowArguments = tl.getDelimitedInput("WorkflowArguments", "\n", true);
            let outputDirectory = tl.getPathInput('CodexOutputRoot', true);
            let codexBootstrapDirectory = path.join(outputDirectory, "bootstrap");
            let toolPath = path.join(codexBootstrapDirectory, "Codex.Automation.Workflow.exe");
            let tool;
            shell.mkdir("-p", codexBootstrapDirectory);
            //download file
            var file = fs.createWriteStream(toolPath);
            yield new Promise(resolve => request.get('https://github.com/Ref12/Codex/releases/download/latest-prerel/Codex.Automation.Workflow.exe').pipe(file).on('finish', resolve));
            tool = tl.tool(toolPath).arg(workflowArguments).arg(`/codexOutputRoot:${outputDirectory}`);
            let rc1 = yield tool.exec();
            console.log('Task done! ' + rc1);
        }
        catch (err) {
            tl.setResult(tl.TaskResult.Failed, err.message);
        }
    });
}
run();
