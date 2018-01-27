let MDACSAuthLoginSwitcher = {};

MDACSAuthLoginSwitcher.StateGenerator = (props) => {
    return {
        showLogin: true,
        user: null,
        alert: null,
        daoAuth: new AuthNetworkDAO(props.authUrl),
    };
};

MDACSAuthLoginSwitcher.Mutators = {
  onCheckLogin: (props, state, setState, username, password) => {
    state.daoAuth.setCredentials(
        username, 
        password
    );

    state.daoAuth.isLoginValid(
        (user) => {
            setState({
                showLogin: false,
                user: user,
                alert: null,
            });
        },
        (res) => {
            setState({
                alert: 'The login failed. Reason given was ' + res + '.',
            });
        },
    );
  },
};

MDACSAuthLoginSwitcher.Views = {
  Main: (props, state, setState, mutators) => {
    const onCheckLogin = mutators.onCheckLogin;

    const top = <div>
                    <img src="utility?logo.png" height="128px" />
                    <MDACSAnonFeedback postUrl="http://kmcg3413.net/mdacs_feedback.py"/>
                </div>;

    if (state.showLogin) {
        let alert_area = null;
        
        if (state.alert !== null) {
            alert_area = <Alert>{state.alert}</Alert>;
        }

        return <div>
            {top}
            <MDACSAuthLogin.ReactComponent 
                onCheckLogin={(u, p) => onCheckLogin(props, state, setState, u, p)} 
            />
            {alert_area}
            </div>;
    } else {
      return <div>
                {top}
                <MDACSDatabaseServiceDirectory.ReactComponent
                  daoDatabase={state.daoAuth.getDatabaseDAO(props.dbUrl)}
                  daoAuth={state.daoAuth}
                  authUrl={props.authUrl}
                  dbUrl={props.dbUrl}
                  />
            </div>;
    }
  },
};

MDACSAuthLoginSwitcher.ReactComponent = class extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSAuthLoginSwitcher.StateGenerator(props);
    }

    render() {
      return MDACSAuthLoginSwitcher.Views.Main(
        this.props,
        this.state,
        this.setState.bind(this),
        MDACSAuthLoginSwitcher.Mutators
      );
    }
}