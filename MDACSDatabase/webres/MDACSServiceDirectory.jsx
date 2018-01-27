MDACSDatabaseServiceDirectory = {};

MDACSDatabaseServiceDirectory.StateGenerator = (props) => {
    return {
        dbdao: props.daoAuth.getDatabaseDAO(props.dbUrl),
    };
};

MDACSDatabaseServiceDirectory.Mutators = {
};

MDACSDatabaseServiceDirectory.Views = {
    Main: (props, state, setState, mutators) => {
        return (<MDACSDatabaseModule dao={props.daoDatabase} />);
    },
};

/// <prop name="dbUrl">The url for database service.</prop>
/// <prop name="authUrl">The url for authentication service</prop>
/// <prop name="daoAuth">DAO for authentication service</prop>
MDACSDatabaseServiceDirectory.ReactComponent = class extends React.Component {
    constructor(props) {
        super(props);

        this.state = MDACSDatabaseServiceDirectory.StateGenerator(props);
    }

    render() {
        return MDACSDatabaseServiceDirectory.Views.Main(
            this.props,
            this.state,
            this.setState.bind(this),
            MDACSDatabaseServiceDirectory.Mutators
        );
    }
}