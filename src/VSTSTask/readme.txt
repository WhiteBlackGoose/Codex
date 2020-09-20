How to regenerate:

1. rev the version in vss-extension.json
2. rev the version in Task\task.json
3. Download nodejs from nodejs.org
4. install tfx cli: npm i -g tfx-cli
5. install typescript: npm install -g typescript
6. npm install
7. tsc
8. from parent directory: tfx extension create --manifest-globs vss-extension.json
9. https://marketplace.visualstudio.com/manage/publishers/ref12 -> Update the extension and publish the VSIX