let MDACSCoreApp = {};

MDACSCoreApp.loadScript = (src, cb) => {
    let scriptElement = document.createElement('script');

    scriptElement.onload = () => cb(true);
    scriptElement.onerror = () => cb(false);
    scriptElement.src = src;

    // document.currentScript.parentNode.insertBefore(scriptElement, document.currentScript);
};

/// <summary>
/// This component handles bootstrapping the rest of the system. It will
/// remotely load any code from other services, if needed, and then load
/// that code here and load those modules into the react component hiarchy.
/// </summary>
MDACSCoreApp.ReactComponent = class extends React.Component {
    constructor(props) {
        super(props);

        this.state = {
            status: 'Loading remote application code packages for login component...',
            scriptsToLoadIndex: 0,
        };
    }

    loadChain() {
        const props = this.props;
        const state = this.state;
        const setState = this.setState.bind(this);

        if (state.scriptsToLoadIndex >= props.scriptsToLoad.length) {
            return;
        }

        // Continue loading the needed scripts.
        let src = props.scriptsToLoad[state.scriptsToLoadIndex];

        MDACSCoreApp.loadScript(src, (success) => {
            this.loadChain();
        });

        setState({
            status: 'Loading script ' + src + '...',
        });
    }

    componentDidMount() {
    }

    render() {
        if (state.status !== null) {
            // We will display the login and once login is successful switch to displaying the avaliable stack.
            // As remote packages are loaded they will become inserted into the stack.
            return <Alert>{state.status}</Alert>;
        }

        // At this point, all scripts have been loaded that were needed.

        return <Alert>scripts loaded</Alert>;
    }
}


/// Bootstrap the application.
request
    .get('./get-config')
    .end((err, res) => {
        if (err) {
            ReactDOM.render(
                <Alert>Uhoh! There was a failure when requesting the application configuration! Try again or wait a few minutes.</Alert>,
                document.getElementById('root')
            );
        } else {
            const cfg = JSON.parse(res.text);

            const scripts = [];
            const components = [
                'MDACSAuthModule.ReactComponent',
                'MDACSDatabaseModule.ReactComponent',
            ];

            ReactDOM.render(
                <MDACSCoreApp.ReactComponent 
                    authUrl={cfg.authUrl}
                    dbUrl={cfg.dbUrl}
                    scripts={scripts}
                    components={components}
                    />
            );            
        }
    });