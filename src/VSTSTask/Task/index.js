"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
const tl = require("vsts-task-lib/task");
const fs = require("fs");
const request = require("request");
const shell = require("shelljs");
const path = require("path");
const q_1 = require("q");
function run() {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            var isWin = tl.osType() === 'Windows_NT'; // /^win/.test(process.platform);
            let workflowArguments = tl.getDelimitedInput("WorkflowArguments", "\n", true);
            let outputDirectory = tl.getPathInput('CodexOutputRoot', true);
            let codexBootstrapDirectory = path.join(outputDirectory, "bootstrap");
            let toolPath = path.join(codexBootstrapDirectory, "Codex.Automation.Workflow.exe");
            let tool;
            shell.mkdir("-p", codexBootstrapDirectory);
            var file = fs.createWriteStream(toolPath);
            yield new Promise(resolve => request.get('https://github.com/Ref12/Codex/releases/download/latest-prerel/Codex.Automation.Workflow.exe').pipe(file).on('finish', resolve));
            if (isWin) {
                tool = tl.tool(toolPath);
            }
            else {
                tool = tl.tool(tl.which('mono', true));
                tool.arg(toolPath);
            }
            tool.arg(workflowArguments);
            tool.arg(`/codexOutputRoot:${outputDirectory}`);
            let delays = [
                500,
                1000,
                2000,
                4000,
                8000
            ];
            for (var i = 0; i < delays.length + 1; i++) {
                try {
                    let rc1 = yield tool.exec();
                    console.log('Task done! ' + rc1);
                    return;
                }
                catch (execError) {
                    if (i < delays.length) {
                        var delayTime = delays[i];
                        console.log('Error launching tool. Trying after ' + delayTime + 'ms:\n' + execError.message);
                        yield q_1.delay(delayTime);
                    }
                    else {
                        throw execError;
                    }
                }
            }
        }
        catch (err) {
            tl.setResult(tl.TaskResult.Failed, err.message);
        }
    });
}
run();
